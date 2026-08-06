using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// High-level connection state of this client, exposed for UI purposes (e.g. showing
/// "Searching for host...", a spinner, a retry button, etc).
/// </summary>
public enum ClientConnectionState
{
    Disconnected,
    Discovering,   // TCP only: listening for the host's multicast broadcast
    Connecting,
    Connected,
    Reconnecting
}

/// <summary>
/// Runs on the mobile client. Establishes and maintains the connection to the Host project,
/// either over TCP (WiFi, discovering the host via its multicast broadcast, matching
/// SignalingServer) or over ADB (USB cable, matching ADBConnectionServer's reverse tunnel).
///
/// Once connected, this only deals with the signaling channel (handshake + SignalingMessage
/// send/receive). Whatever builds the actual WebRTC peer on this side should subscribe to
/// OnSignalingMessageReceived / OnConnected / OnDisconnected instead of touching sockets directly.
/// </summary>
public class StreamManager : MonoBehaviour
{
    public static StreamManager Instance { get; private set; }

    #region Variables

    [Header("General")]
    [SerializeField] private ClientType clientType = ClientType.PLAYER;
    [SerializeField] private bool debug = false;

    [Header("TCP discovery (matches SignalingServer)")]
    [SerializeField] private int broadcastPort = 8053;
    private const string MulticastGroup = "239.0.0.1";
    [SerializeField] private int discoveryTimeoutMs = 8000;
    [SerializeField] private int tcpConnectMaxRetries = 5;
    [SerializeField] private int tcpConnectRetryDelayMs = 1500;

    [Header("ADB (matches ADBConnectionServer)")]
    [SerializeField] private int adbRemotePort = 7778;
    [SerializeField] private int adbConnectMaxRetries = 6;
    [SerializeField] private int adbConnectRetryDelayMs = 1000;

    [Header("Reconnection")]
    [SerializeField] private bool autoReconnect = true;
    [SerializeField] private int reconnectDelayMs = 2000;
    [SerializeField] private int maxReconnectAttempts = 5;

    public ClientConnectionState State { get; private set; } = ClientConnectionState.Disconnected;
    public ConnectionTransport CurrentTransport { get; private set; } = ConnectionTransport.NONE;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread receiveThread;

    private volatile bool connectionActive;
    private volatile bool intentionalDisconnect;

    public event Action OnConnected;
    public event Action<string> OnDisconnected;              // reason
    public event Action<string> OnConnectionError;            // error message
    public event Action<ClientConnectionState> OnStateChanged;
    public event Action<SignalingMessage> OnSignalingMessageReceived;

    #endregion

    #region Public API

    /// <summary>
    /// Connects over WiFi: discovers the host via UDP multicast, then opens a TCP socket to it.
    /// </summary>
    public void ConnectViaTCP()
    {
        if (State == ClientConnectionState.Connecting || State == ClientConnectionState.Connected)
        {
            LogWarning("ConnectViaTCP ignored, a connection attempt is already in progress.");
            return;
        }

        intentionalDisconnect = false;
        CurrentTransport = ConnectionTransport.TCP;
        SetState(ClientConnectionState.Discovering);

        new Thread(TCPConnectionRoutine) { IsBackground = true, Name = "TCP Connect" }.Start();
    }

    /// <summary>
    /// Connects over USB: assumes the host already ran "adb reverse" for this device, and
    /// simply opens a TCP socket to localhost:adbRemotePort on this device.
    /// </summary>
    public void ConnectViaADB()
    {
        if (State == ClientConnectionState.Connecting || State == ClientConnectionState.Connected)
        {
            LogWarning("ConnectViaADB ignored, a connection attempt is already in progress.");
            return;
        }

        intentionalDisconnect = false;
        CurrentTransport = ConnectionTransport.ADB;
        SetState(ClientConnectionState.Connecting);

        new Thread(ADBConnectionRoutine) { IsBackground = true, Name = "ADB Connect" }.Start();
    }

    /// <summary>
    /// Cleanly closes the current connection, telling the host first.
    /// </summary>
    public void Disconnect()
    {
        intentionalDisconnect = true;
        CloseConnection("Disconnected by user.");
    }

    /// <summary>
    /// Sends a signaling message (SDP/ICE) to the host, regardless of the active transport.
    /// </summary>
    public bool SendSignalingMessage(SignalingMessage msg)
    {
        if (!connectionActive || stream == null) return false;

        try
        {
            NetworkUtils.WriteFramedMessage(stream, JsonUtility.ToJson(msg), syncRoot: stream);
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Error sending message: {ex.Message}");
            HandleConnectionLost($"Send failed: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region TCP connection

    private void TCPConnectionRoutine()
    {
        if (!TryDiscoverHost(out string hostIP, out int hostPort))
        {
            DispatchError("Could not find the host on the network (discovery timed out).");
            SetState(ClientConnectionState.Disconnected);
            return;
        }

        EstablishTCPConnection(hostIP, hostPort);
    }

    /// <summary>
    /// Listens on the host's multicast group until a BROADCAST ConnectionData arrives,
    /// or discoveryTimeoutMs runs out.
    /// </summary>
    private bool TryDiscoverHost(out string hostIP, out int hostPort)
    {
        hostIP = null;
        hostPort = 0;

        UdpClient listener = null;
        try
        {
            listener = new UdpClient();
            listener.ExclusiveAddressUse = false;
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, broadcastPort));
            listener.JoinMulticastGroup(IPAddress.Parse(MulticastGroup));
            listener.Client.ReceiveTimeout = discoveryTimeoutMs;

            Log($"Listening for host broadcast on {MulticastGroup}:{broadcastPort}...");

            var remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = listener.Receive(ref remoteEP);
            string json = Encoding.UTF8.GetString(data);

            ConnectionData decoded = JsonUtility.FromJson<ConnectionData>(json);
            if (decoded == null || decoded.connType != ConnectionEvent.BROADCAST || string.IsNullOrEmpty(decoded.ipAddress))
            {
                LogWarning("Received a broadcast packet that wasn't a valid host announcement.");
                return false;
            }

            hostIP = decoded.ipAddress;
            hostPort = decoded.port;
            Log($"Host found at {hostIP}:{hostPort}.");
            return true;
        }
        catch (SocketException ex)
        {
            // Most common case: ReceiveTimeout expired without any broadcast arriving.
            LogWarning($"Discovery failed (timeout or socket error): {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            LogError($"Unexpected discovery error: {ex.Message}");
            return false;
        }
        finally
        {
            try { listener?.Close(); } catch { }
        }
    }

    /// <summary>
    /// Opens a TCP socket to the host's SignalingServer and completes the handshake, retrying
    /// on failure (e.g. the host briefly not listening yet).
    /// </summary>
    private void EstablishTCPConnection(string hostIP, int hostPort)
    {
        SetState(ClientConnectionState.Connecting);

        for (int attempt = 1; attempt <= tcpConnectMaxRetries; attempt++)
        {
            if (intentionalDisconnect) return;

            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(hostIP, hostPort);
                stream = tcpClient.GetStream();

                SendHandshake(stream);

                connectionActive = true;
                SetState(ClientConnectionState.Connected);
                Log($"Connected to host via TCP ({hostIP}:{hostPort}).");
                DispatchConnected();

                StartReceiveLoop();
                return;
            }
            catch (Exception ex)
            {
                LogWarning($"TCP connect attempt {attempt}/{tcpConnectMaxRetries} failed: {ex.Message}");
                CleanupSocket();
                Thread.Sleep(tcpConnectRetryDelayMs);
            }
        }

        DispatchError("Could not establish a TCP connection to the host.");
        SetState(ClientConnectionState.Disconnected);
    }

    #endregion

    #region ADB connection

    private void ADBConnectionRoutine()
    {
        EstablishADBConnection();
    }

    /// <summary>
    /// Connects to 127.0.0.1:adbRemotePort, which the host tunnels back to itself via
    /// "adb reverse". Retries while the host might still be finishing that setup.
    /// </summary>
    private void EstablishADBConnection()
    {
        SetState(ClientConnectionState.Connecting);

        for (int attempt = 1; attempt <= adbConnectMaxRetries; attempt++)
        {
            if (intentionalDisconnect) return;

            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(IPAddress.Loopback, adbRemotePort);
                stream = tcpClient.GetStream();

                SendHandshake(stream);

                connectionActive = true;
                SetState(ClientConnectionState.Connected);
                Log("Connected to host via ADB (USB).");
                DispatchConnected();

                StartReceiveLoop();
                return;
            }
            catch (Exception ex)
            {
                LogWarning($"ADB connect attempt {attempt}/{adbConnectMaxRetries} failed: {ex.Message}");
                CleanupSocket();
                Thread.Sleep(adbConnectRetryDelayMs);
            }
        }

        DispatchError("Could not connect over ADB. Check the cable, USB debugging, and that the host has 'adb reverse' set up.");
        SetState(ClientConnectionState.Disconnected);
    }

    #endregion

    #region Shared protocol handling

    private void SendHandshake(NetworkStream targetStream)
    {
        string myIP = GetLocalIPSafe();
        ConnectionData handshake = ConnectionData.ForHandshake(myIP, clientType);
        NetworkUtils.WriteFramedMessage(targetStream, JsonUtility.ToJson(handshake), syncRoot: targetStream);
    }

    private string GetLocalIPSafe()
    {
        try { return NetworkUtils.GetIP(); }
        catch { return "0.0.0.0"; }
    }

    private void StartReceiveLoop()
    {
        receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "StreamManagerClient Receive" };
        receiveThread.Start();
    }

    private void ReceiveLoop()
    {
        try
        {
            while (connectionActive)
            {
                if (!NetworkUtils.TryReadFramedMessage(stream, out string incoming))
                    break; // host closed the connection

                SignalingMessage msg = JsonUtility.FromJson<SignalingMessage>(incoming);

                if (msg.type == ConnectionEvent.DISCONNECT)
                {
                    HandleConnectionLost("Host requested disconnection.");
                    return;
                }

                SignalingMessage capturedMsg = msg;
                UnityMainThreadDispatcher.Instance().Enqueue(() => OnSignalingMessageReceived?.Invoke(capturedMsg));
            }

            if (connectionActive)
                HandleConnectionLost("Connection closed by host.");
        }
        catch (Exception ex)
        {
            if (connectionActive)
                HandleConnectionLost($"Connection error: {ex.Message}");
        }
    }

    #endregion

    #region Disconnection & reconnection

    /// <summary>
    /// Called when the connection drops unexpectedly (as opposed to Disconnect(), which sets
    /// intentionalDisconnect first). Cleans up and optionally kicks off a reconnect.
    /// </summary>
    private void HandleConnectionLost(string reason)
    {
        if (!connectionActive) return; // already being handled by another path

        ConnectionTransport transportAtDrop = CurrentTransport;
        CloseConnection(reason);

        if (!intentionalDisconnect && autoReconnect)
            AttemptReconnect(transportAtDrop);
    }

    private void AttemptReconnect(ConnectionTransport transport)
    {
        SetState(ClientConnectionState.Reconnecting);
        new Thread(() => ReconnectRoutine(transport)) { IsBackground = true, Name = "Reconnect" }.Start();
    }

    private void ReconnectRoutine(ConnectionTransport transport)
    {
        for (int attempt = 1; attempt <= maxReconnectAttempts; attempt++)
        {
            if (intentionalDisconnect) return;

            Log($"Reconnect attempt {attempt}/{maxReconnectAttempts} ({transport})...");
            Thread.Sleep(reconnectDelayMs);

            if (transport == ConnectionTransport.ADB)
            {
                EstablishADBConnection();
            }
            else
            {
                // Re-discover instead of reusing the last known IP: network conditions
                // (or the host's IP itself) may have changed while we were disconnected.
                if (TryDiscoverHost(out string hostIP, out int hostPort))
                    EstablishTCPConnection(hostIP, hostPort);
            }

            if (State == ClientConnectionState.Connected) return;
        }

        DispatchError("Reconnection failed after several attempts.");
        SetState(ClientConnectionState.Disconnected);
    }

    /// <summary>
    /// Closes sockets/threads and notifies listeners. Safe to call even if already disconnected.
    /// </summary>
    private void CloseConnection(string reason)
    {
        bool wasActive = connectionActive;
        connectionActive = false;

        if (wasActive && intentionalDisconnect && stream != null)
        {
            try { SendSignalingMessage(new SignalingMessage(ConnectionEvent.DISCONNECT, null)); } catch { }
        }

        CleanupSocket();

        if (wasActive)
        {
            SetState(ClientConnectionState.Disconnected);
            Log($"Disconnected: {reason}");
            UnityMainThreadDispatcher.Instance().Enqueue(() => OnDisconnected?.Invoke(reason));
        }
    }

    private void CleanupSocket()
    {
        try { stream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }
        stream = null;
        tcpClient = null;
    }

    #endregion

    #region State & logging helpers

    private void SetState(ClientConnectionState newState)
    {
        State = newState;
        UnityMainThreadDispatcher.Instance().Enqueue(() => OnStateChanged?.Invoke(newState));
    }

    private void DispatchConnected()
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => OnConnected?.Invoke());
    }

    private void DispatchError(string message)
    {
        LogError(message);
        UnityMainThreadDispatcher.Instance().Enqueue(() => OnConnectionError?.Invoke(message));
    }

    private void Log(string msg) { if (debug) Debug.Log($"[StreamManagerClient] {msg}"); }
    private void LogWarning(string msg) { if (debug) Debug.LogWarning($"[StreamManagerClient] {msg}"); }
    private void LogError(string msg) { Debug.LogError($"[StreamManagerClient] {msg}"); }

    #endregion

    #region Monobehaviour

    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        intentionalDisconnect = true;
        try { CloseConnection("Client shutting down."); } catch { }
    }

    #endregion
}