using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
    #region Variables
    /// <summary>
    /// Instance of ConnectionManager (singleton)
    /// </summary>
    public static ConnectionManager Instance { get; private set; }

    /// <summary>
    /// Variable that indicates whether we should print debug information or not
    /// </summary>
    [SerializeField] private bool debug = false;

    /// <summary>
    /// Current of status of the connection
    /// </summary>
    public ClientConnectionState currentState { get; private set; } = ClientConnectionState.Disconnected;

    /// <summary>
    /// Current form of transport selected
    /// </summary>
    public ClientConnectionType currentTransport { get; private set; } = ClientConnectionType.NONE;

    /// <summary>
    /// Port used for adb communication. Even when it says "remote" it actually is a port of this device (host listen on this port thanks to adb reverse tunnel)
    /// </summary>
    [SerializeField] private int adbRemotePort = 7778;
    /// <summary>
    /// Number of maximun attemps to establish adb connection
    /// </summary>
    [SerializeField] private int adbConnectMaxRetries = 5;
    /// <summary>
    /// Time between adb connection attemps
    /// </summary>
    [SerializeField] private int adbConnectRetryDelayMs = 1500;

    /// <summary>
    /// Time between adb reverse tunnel checks
    /// </summary>
    [SerializeField] private float tunnelCheckIntervalSeconds = 1f;
    /// <summary>
    /// Time we spend checking adb reverse tunnel in each attemp
    /// </summary>
    [SerializeField] private int tunnelProbeTimeoutMs = 500;
    /// <summary>
    /// Variable that indicates whether we have an adb reverse tunnel established or not
    /// </summary>
    public bool tunnelEstablished { get; private set; }
    /// <summary>
    /// Thread where we check for adb tunnel
    /// </summary>
    private Thread tunnelWatcherThread;
    /// <summary>
    /// Variable that indicates whether we are currently checking for adb tunnel or not
    /// </summary>
    private volatile bool tunnelWatcherRunning;

    /// <summary>
    /// Port where this device will listen to upcoming network data during TCP connection
    /// </summary>
    private int listenPort = 8053;
    /// <summary>
    /// Number of maximun attemps to establish tcp connection
    /// </summary>
    [SerializeField] private int tcpConnectMaxRetries = 5;
    /// <summary>
    /// Time between tcp connection attemps
    /// </summary>
    [SerializeField] private int tcpConnectRetryDelayMs = 1500;
    /// <summary>
    /// Time we spend trying to establish TCP connection in each attemp
    /// </summary>
    [SerializeField] private int tcpConnectTimeoutMs = 1500;
    /// <summary>
    /// Multicast IP group for specific broadcasting
    /// </summary>
    private const string MulticastGroup = "239.0.0.1";
    /// <summary>
    /// Listener used for multicast listen of devices
    /// </summary>
    private UdpClient multicastListener;
    /// <summary>
    /// Thread where the multicast messages will be processed
    /// </summary>
    private Thread multicastListenThread;


    /// <summary>
    /// Variable that indicates whether we are waiting for handshake response or not
    /// </summary>
    private volatile bool handshaking;
    /// <summary>
    /// Time we spend waiting for handshake response
    /// </summary>
    [SerializeField] private int handshakeTimeoutMs = 3000;
    /// <summary>
    /// Variable that indicates whether we disconnected intentionally or not
    /// </summary>
    private volatile bool intentionalDisconnect;
    /// <summary>
    /// Host device's port for communication
    /// </summary>
    private int hostPort;
    /// <summary>
    /// IP of host device
    /// </summary>
    private string hostIP;
    /// <summary>
    /// Host's TCP connection
    /// </summary>
    private TcpClient hostConnection;
    /// <summary>
    /// Thread where we communicate with host device
    /// </summary>
    private Thread readThread;
    /// <summary>
    /// TCP stream from where message exchange (mainly handshake, ICE and SDP offers) will happen
    /// </summary>
    private NetworkStream stream;

    /// <summary>
    /// Variable that indicates whether the server is running or not
    /// </summary>
    private bool running;
    /// <summary>
    /// Variable that indicates whether this machine is connected to a host or not
    /// </summary>
    public bool connected { get; private set; } = false;
    /// <summary>
    /// Variable that grants access to network stream
    /// </summary>
    private readonly object socketLock = new object();
    /// <summary>
    /// This machine's IP
    /// </summary>
    private string ipAddress;

    /// <summary>
    /// Structure that grants access to ConnectionData of devices found (via multicast) by its IP
    /// </summary>
    private Dictionary<string, ConnectionData> sessions = new Dictionary<string, ConnectionData>();

    /// <summary>
    /// Reference to prefab object that represents this client in the communication with host
    /// </summary>
    [SerializeField] private GameObject clientPrefab = null;
    /// <summary>
    /// Component that allows the WebRTC communication
    /// </summary>
    private HIMUReceiver receiver = null;
    #endregion

    #region TCP
    /// <summary>
    /// Configures Udp listener and starts session discovery thread
    /// </summary>
    public void StartListening()
    {
        if (!connected)
        {
            if(debug) Debug.Log("[StreamManager] Launching TCP session discovery");
            running = true;

            multicastListener = new UdpClient();
            multicastListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            multicastListener.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            multicastListener.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse(MulticastGroup), IPAddress.Any));

            multicastListenThread = new Thread(MulticastListenLoop) { IsBackground = true, Name = "StreamManager TCP discovering loop" };
            multicastListenThread.Start();
        }
    }

    /// <summary>
    /// Session discovery thread
    /// </summary>
    private void MulticastListenLoop()
    {
        if (debug)
            UnityMainThreadDispatcher.Instance().Enqueue(()
                => Debug.Log($"[StreamManager] Launched loop - {running} / {connected}"));

        while (running && !connected)
        {
            try
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = multicastListener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);

                if (string.IsNullOrEmpty(message))
                {
                    if (debug) Debug.LogWarning("[StreamManager] Ignoring non-valid UDP package");
                    continue;
                }

                ConnectionData decodedData = JsonUtility.FromJson<ConnectionData>(message);
                if (decodedData.connEvent != ConnectionEvent.MULTICAST)
                    continue;

                if (debug) UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log("[StreamManager] Session found via MULTICAST"));
                StoreSession(decodedData);
            }
            catch (SocketException)
            {
                break;
            }
            catch (Exception e)
            {
                if (running)
                {
                    if (debug) Debug.LogWarning($"[StreamManager] TCP session discovery thread error: {e.Message}");
                    CloseConnection();
                }
            }
        }
    }

    /// <summary>
    /// Initiates a TCP connection attemp adn changes status accordingly
    /// </summary>
    /// <param name="data"></param>
    public void ConnectViaTCP(ConnectionData data)
    {
        if (currentState == ClientConnectionState.Connecting || currentState == ClientConnectionState.Connected) {
            if (debug) Debug.LogWarning("[StreamManager] ConnectViaTCP ignored because other is already in progress");
            return; 
        }

        intentionalDisconnect = false;
        currentTransport = ClientConnectionType.TCP;
        currentState = ClientConnectionState.Connecting;
        hostIP = data.ipAddress;
        hostPort = data.port;
        UIManager.Instance.ConnectionAttemp();

        new Thread(() => TryTCPConnection(data)) { IsBackground = true, Name = "TCP Connect" }.Start();
    }

    /// <summary>
    /// Tries to establish a TCP connection to teh given session (host)
    /// </summary>
    /// <param name="data">Session information given to attemp connection</param>
    private void TryTCPConnection(ConnectionData data)
    {
        for (int attempt = 1; attempt <= tcpConnectMaxRetries; attempt++)
        {
            if (intentionalDisconnect) return;

            try
            {
                if (debug) Debug.Log($"[StreamManager] TCP connection attempt {attempt}/{tcpConnectMaxRetries} to {hostIP}:{hostPort}");

                hostConnection = new TcpClient();
                var result = hostConnection.BeginConnect(hostIP, hostPort, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(tcpConnectTimeoutMs);

                if (!success || !hostConnection.Connected)
                {
                    throw new SocketException((int)SocketError.TimedOut);
                }

                hostConnection.EndConnect(result);

                lock (socketLock)
                {
                    stream = hostConnection.GetStream();
                }

                currentTransport = ClientConnectionType.TCP;

                if (!Handshake(stream))
                {
                    CleanupStream();
                    currentTransport = ClientConnectionType.NONE;
                    if (attempt < tcpConnectMaxRetries) Thread.Sleep(tcpConnectRetryDelayMs);
                    continue;
                }

                intentionalDisconnect = false;
                if (debug) Debug.Log("[StreamManager] Connected to host via TCP.");

                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    OnConnectionStarted();
                    UIManager.Instance.ConnectionSuccessful();
                });
                return;
            }
            catch (SocketException se)
            {
                if (debug) Debug.LogWarning($"TCP connect attempt {attempt}/{tcpConnectMaxRetries} failed: {se.Message}");
                CleanupStream();
                if (attempt < tcpConnectMaxRetries) Thread.Sleep(tcpConnectRetryDelayMs);
            }
            catch (Exception ex)
            {
                if (debug) Debug.LogError($"[StreamManager] Unexpected error during TCP connect: {ex}");
                CleanupStream();
                break;
            }
        }

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            if (debug) Debug.LogError($"[StreamManager] Could not connect via TCP to {hostIP}:{hostPort} after {tcpConnectMaxRetries} attempts.");
            RemoveSession(data);
            CloseConnection();
        });
    }
    #endregion

    #region ADB
    /// <summary>
    ///  Initiates an ADB connection attemp adn changes status accordingly
    /// </summary>
    public void ConnectViaADB()
    {
        if (currentState == ClientConnectionState.Connecting || currentState == ClientConnectionState.Connected)
        {
            if (debug) Debug.LogWarning("[StreamManager] ConnectViaADB ignored, a connection attempt is already in progress");
            return;
        }

        intentionalDisconnect = false;
        currentTransport = ClientConnectionType.ADB;
        currentState = ClientConnectionState.Connecting;
        UIManager.Instance.ConnectionAttemp();

        new Thread(TryADBConnection) { IsBackground = true, Name = "ADB Connect" }.Start();
    }

    /// <summary>
    /// Tries to establish an adb connection to teh given session (host)
    /// </summary>
    private void TryADBConnection()
    {
        for (int attempt = 1; attempt <= adbConnectMaxRetries; attempt++)
        {
            if (intentionalDisconnect) return;
            try
            {
                hostConnection = new TcpClient();
                hostConnection.Connect(IPAddress.Loopback, adbRemotePort);
                lock (socketLock)
                {
                    stream = hostConnection.GetStream();
                }

                currentTransport = ClientConnectionType.ADB;
                
                if (!Handshake(stream, expectedHostPort: adbRemotePort))
                {
                    CleanupStream();
                    currentTransport = ClientConnectionType.NONE;
                    if (attempt < adbConnectMaxRetries) Thread.Sleep(adbConnectRetryDelayMs);
                    continue;
                }

                intentionalDisconnect = false;
                if (debug) Debug.Log("[StreamManager] Connected to host via ADB (USB).");

                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    OnConnectionStarted();
                    UIManager.Instance.ConnectionSuccessful();
                });
                return;
            }
            catch (SocketException se)
            {
                if (debug) Debug.LogWarning($"TCP connect attempt {attempt}/{adbConnectMaxRetries} failed: {se.Message}");
                CleanupStream();
                if (attempt < adbConnectMaxRetries) Thread.Sleep(adbConnectRetryDelayMs);
            }
            catch (Exception ex)
            {
                if (debug) Debug.LogError($"[StreamManager] Unexpected error during TCP connect: {ex}");
                CleanupStream();
                break;
            }
        }

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            if (debug) Debug.LogError("Could not connect over ADB. Check the cable, USB debugging, and that the host has 'adb reverse' set up.");
            UIManager.Instance.ConnectionFailed(ipAddress);
            CloseConnection();
        });
    }

    /// <summary>
    /// Launches process that checks if reverse tunnel is active
    /// </summary>
    private void StartTunnelWatcher()
    {
        StopTunnelWatcher();

        tunnelEstablished = false;
        tunnelWatcherRunning = true;
        tunnelWatcherThread = new Thread(TunnelWatcherLoop) { IsBackground = true, Name = "ADB Tunnel Watcher" };
        tunnelWatcherThread.Start();
    }

    /// <summary>
    /// Stops process that checks if reverse tunnel is active
    /// </summary>
    private void StopTunnelWatcher()
    {
        tunnelWatcherRunning = false;
        tunnelWatcherThread?.Join(500);
        tunnelWatcherThread = null;
    }

    /// <summary>
    /// Secundary process that checks if an adb reverse tunnel was established by host and udpates flag. Stops once a connection is established
    /// </summary>
    private void TunnelWatcherLoop()
    {
        while (tunnelWatcherRunning && !connected && !intentionalDisconnect)
        {
            bool tunnelUp = CheckReverseTunnel();
            tunnelEstablished = tunnelUp;

            if (tunnelUp && debug)
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        Debug.Log("[StreamManager] ADB reverse tunnel detected, waiting for handshake..."));
            }

            else if (!tunnelUp && handshaking)
            {
                if (debug) UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    Debug.LogWarning("[StreamManager] Tunnel lost while waiting for handshake ACK, aborting attempt."));
                CleanupStream();
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        UIManager.Instance.UpdateADBButton(tunnelEstablished));

            int intervalMs = Mathf.Max(200, Mathf.RoundToInt(tunnelCheckIntervalSeconds * 1000f));
            int waited = 0;
            while (waited < intervalMs && tunnelWatcherRunning && !connected && !intentionalDisconnect)
            {
                Thread.Sleep(200);
                waited += 200;
            }
        }
    }

    /// <summary>
    /// Opens TCP connection to localhost:adbRemotePort to check if there is a tunnel established
    /// </summary>
    private bool CheckReverseTunnel()
    {
        try
        {
            using (TcpClient probe = new TcpClient())
            {
                var result = probe.BeginConnect(IPAddress.Loopback, adbRemotePort, null, null);
                bool ok = result.AsyncWaitHandle.WaitOne(tunnelProbeTimeoutMs) && probe.Connected;
                if (ok) probe.EndConnect(result);
                return ok;
            }
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region Shared Methods
    /// <summary>
    /// Sends 'Handshake' message to host and waits for response from host
    /// </summary>
    /// <param name="netStream">Netwrok stream to communicate with host</param>
    /// <param name="expectedHostPort">Port expected from host for communication (confirmation)</param>
    /// <returns>Whether handshake exchange was succesful or not</returns>
    private bool Handshake(NetworkStream netStream, int expectedHostPort = -1)
    {
        var currentStream = GetStreamSafe();
        if (currentStream == null)
        {
            if (debug) Debug.LogWarning("[StreamManager] SendHandshake ignored, stream is not available");
            return false;
        }

        string json = JsonUtility.ToJson(ConnectionData.ForHandshake(ipAddress, currentTransport));
        NetworkUtils.WriteFramedMessage(currentStream, json);
        if (debug) Debug.Log($"[StreamManager] Handshake sent to {hostIP}:{hostPort}");

        handshaking = true;
        int previousTimeout = netStream.ReadTimeout;
        try
        {
            netStream.ReadTimeout = handshakeTimeoutMs;

            if (!NetworkUtils.TryReadFramedMessage(netStream, out string ackJson))
            {
                if (debug) Debug.LogWarning("[StreamManager] Host closed the connection during handshake.");
                return false;
            }

            ConnectionData ack = JsonUtility.FromJson<ConnectionData>(ackJson);

            if (ack.connEvent != ConnectionEvent.HANDSHAKE)
            {
                if (debug) Debug.LogWarning($"[StreamManager] Expected HANDSHAKE_ACK, got {ack.connEvent} instead.");
                return false;
            }

            if (expectedHostPort >= 0 && ack.port != expectedHostPort)
            {
                if (debug) Debug.LogWarning($"[StreamManager] Host reported port {ack.port} but we connected " +
                    $"expecting {expectedHostPort}. Check that adbRemotePort/remotePort match between projects.");
            }

            if (debug) Debug.Log($"[StreamManager] Handshake received from host, connection established");
            return true;
        }
        catch (IOException)
        {
            if (debug) Debug.LogWarning("[StreamManager] Timed out waiting for HANDSHAKE_ACK.");
            return false;
        }
        catch (ObjectDisposedException)
        {
            if (debug) Debug.LogWarning("[StreamManager] Handshake aborted: tunnel dropped while waiting for the ACK.");
            return false;
        }
        finally
        {
            handshaking = false;
            try { netStream.ReadTimeout = previousTimeout; }
            catch { }
        }
    }

    /// <summary>
    /// What to do once a connection is established
    /// </summary>
    public void OnConnectionStarted()
    {
        connected = true;
        currentState = ClientConnectionState.Connected;
        GameObject client = Instantiate(clientPrefab);
        client.transform.position = Vector3.zero;
        receiver = client.GetComponent<HIMUReceiver>();
        receiver.SetUpAndInitialize(SendMessage);

        readThread = new Thread(ReadLoop) { IsBackground = true };
        readThread.Start();
        StopTunnelWatcher();
        CleanupListener();
    }

    /// <summary>
    /// Grants access to network stream safely
    /// </summary>
    /// <returns></returns>
    private NetworkStream GetStreamSafe()
    {
        lock (socketLock)
        {
            return stream;
        }
    }

    /// <summary>
    /// Process that keeps running listening for messages
    /// </summary>
    private void ReadLoop()
    {
        while (running)
        {
            NetworkStream currentStream = GetStreamSafe();
            if (currentStream == null) break;

            try
            {
                if (!NetworkUtils.TryReadFramedMessage(currentStream, out string json))
                {
                    if (!intentionalDisconnect)
                        UnityMainThreadDispatcher.Instance().Enqueue(OnConnectionLost);
                    break;
                }

                var msg = JsonUtility.FromJson<SignalingMessage>(json);
                UnityMainThreadDispatcher.Instance()?.Enqueue(() => HandleMessage(msg));
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                if (!intentionalDisconnect) UnityMainThreadDispatcher.Instance().Enqueue(OnConnectionLost);
                break;
            }
        }
    }

    /// <summary>
    /// Process messages for WebRTC configuration
    /// </summary>
    /// <param name="msg">Message to process</param>
    private void HandleMessage(SignalingMessage msg)
    {
        if (msg.type == ConnectionEvent.SDP)
        {
            SessionDescriptionData data = JsonUtility.FromJson<SessionDescriptionData>(msg.body);
            RTCSessionDescription offer = data.ToRTCDesc();
            StartCoroutine(receiver.HandleOffer(offer));
        }
        else if (msg.type == ConnectionEvent.ICE)
        {
            IceCandidateData data = JsonUtility.FromJson<IceCandidateData>(msg.body);
            RTCIceCandidateInit init = new RTCIceCandidateInit
            {
                candidate = data.candidate,
                sdpMid = data.sdpMid,
                sdpMLineIndex = data.sdpMLineIndex
            };
            receiver.AddIceCandidate(init);
        }
    }

    /// <summary>
    /// Sends a message to host
    /// </summary>
    /// <param name="msg">Message to send</param>
    private void SendMessage(SignalingMessage msg)
    {
        var currentStream = GetStreamSafe();
        if (currentStream == null)
        {
            if (debug) Debug.LogWarning("[StreamManager] SendMessage ignored, stream is not available");
            return;
        }

        try
        {
            NetworkUtils.WriteFramedMessage(currentStream, JsonUtility.ToJson(msg));
        }
        catch (Exception e)
        {
            if (debug) Debug.LogWarning($"[StreamManager] SendMessage failed: {e.Message}");
        }
    }
    #endregion

    #region Session management
    /// <summary>
    /// Adds the new session discovered and creates an object on scene via UIManager
    /// </summary>
    /// <param name="data">Connection data of the new session discovered</param>
    private void StoreSession(ConnectionData data)
    {
        if (sessions.TryGetValue(data.ipAddress, out ConnectionData duplicatedSession)) return;

        sessions.Add(data.ipAddress, data);
        UnityMainThreadDispatcher.Instance().Enqueue(() => UIManager.Instance?.AddNewSessionUI(data));
    }

    /// <summary>
    /// Removes a session that is no longer available
    /// </summary>
    /// <param name="data">ConnectionData of the session to remove</param>
    private void RemoveSession(ConnectionData data)
    {
        sessions.Remove(data.ipAddress);
    }
    #endregion

    #region CleanUp
    /// <summary>
    /// Method called for intentional disconnection
    /// </summary>
    public void Disconnect()
    {
        intentionalDisconnect = true;
        CloseConnection();
    }

    /// <summary>
    /// Method called when connection is lost abruptly
    /// </summary>
    private void OnConnectionLost()
    {
        intentionalDisconnect = false;
        CloseConnection();
        AppManager.Instance.ConnectionLost();
    }

    /// <summary>
    /// Closure of all structures used on communication. If it was intentional we send a DISCONNECT message before
    /// </summary>
    private void CloseConnection()
    {
        if (connected && intentionalDisconnect && GetStreamSafe() != null)
        {
            try { SendMessage(new SignalingMessage(ConnectionEvent.DISCONNECT, null)); } catch { }
        }

        CleanupStream();
        if (currentTransport == ClientConnectionType.ADB)
            CleanupADB();

        connected = false;
        currentState = ClientConnectionState.Disconnected;
        currentTransport = ClientConnectionType.NONE;
    }

    /// <summary>
    /// Cleans all structures of an ADB connection
    /// </summary>
    private void CleanupADB()
    {
        StopTunnelWatcher();
        tunnelEstablished = false;
    }

    /// <summary>
    /// Cleans all structures used on TCP connection (both cases)
    /// </summary>
    private void CleanupStream()
    {
        NetworkStream streamToClose;
        TcpClient connectionToClose;

        lock (socketLock)
        {
            streamToClose = stream;
            connectionToClose = hostConnection;
            stream = null;
            hostConnection = null;
        }

        try { streamToClose?.Close(); } catch { }
        try { connectionToClose?.Close(); } catch { }

        readThread?.Join(500);
    }

    /// <summary>
    /// Cleans multicast listener (in charge of session discovery)
    /// </summary>
    private void CleanupListener()
    {
        try { multicastListener?.Close(); } catch { }
        multicastListener = null;
        multicastListenThread?.Join(500);
    }
    #endregion

    #region Monobehaviour
    private void Awake()
    {
        if (Instance) {
            try { Destroy(Instance.gameObject); }
            catch { if (debug) Debug.Log("No se pudo borrar el objeto del singleton"); }
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        running = false;
        connected = false;
        ipAddress = NetworkUtils.GetIP();

        StartCoroutine(WebRTC.Update());
    }

    private void Start()
    {
        StartListening();
        StartTunnelWatcher();
    }

    private void OnDestroy()
    {
        running = false;
        StopTunnelWatcher();
        CloseConnection();
        CleanupListener();
        if (receiver) Destroy(receiver.gameObject);
    }
    #endregion
}