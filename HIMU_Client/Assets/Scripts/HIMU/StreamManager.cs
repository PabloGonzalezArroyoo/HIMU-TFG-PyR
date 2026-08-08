using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;

public class StreamManager : MonoBehaviour
{
    #region Variables
    public static StreamManager Instance { get; private set; }

    [SerializeField]
    private bool debug = false;

    public ClientConnectionState currentState { get; private set; } = ClientConnectionState.Disconnected;
    public ConnectionTransport currentTransport { get; private set; } = ConnectionTransport.NONE;

    [SerializeField] private int adbRemotePort = 7778;
    [SerializeField] private int adbConnectMaxRetries = 6;
    [SerializeField] private int adbConnectRetryDelayMs = 1000;

    [SerializeField] private int tcpConnectMaxRetries = 6;
    [SerializeField] private int tcpConnectRetryDelayMs = 1000;
    [SerializeField] private int tcpConnectTimeoutMs = 3000;

    private volatile bool intentionalDisconnect;

    /// <summary>
    /// Wether the server is running or not.
    /// </summary>
    private bool running;

    /// <summary>
    /// Wether this machine is connected to a host or not.
    /// </summary>
    public bool connected { get; private set; } = false;

    /// <summary>
    /// What type of client this device is (STREAM or PLAYER, NONE = non existent device).
    /// </summary>
    [SerializeField]
    private ClientType clientType;

    /// <summary>
    /// Listener used for broadcast search of devices.
    /// </summary>
    private UdpClient listener;

    /// <summary>
    /// Thread where the broadcast will be done.
    /// </summary>
    private Thread listenThread;

    /// <summary>
    /// Host's TCP connection
    /// </summary>
    private TcpClient hostConnection;

    /// <summary>
    /// TCP stream from where the communication (mainly on handshake, ICE and SDP offers) will happen.
    /// </summary>
    private NetworkStream stream;

    /// <summary>
    /// Thread where the communication will happen.
    /// </summary>
    private Thread readThread;

    private readonly object socketLock = new object();

    /// <summary>
    /// Port where this device will listen to upcoming network data.
    /// </summary>
    private int listenPort = 8053;

    /// <summary>
    /// Port where the host is located.
    /// </summary>
    private int hostPort;

    /// <summary>
    /// Host's IP.
    /// </summary>
    private string hostIP;

    /// <summary>
    /// This machine's IP.
    /// </summary>
    private string ipAddress;

    /// <summary>
    /// Multicast IP group for specific broadcasting.
    /// </summary>
    private const string MulticastGroup = "239.0.0.1";


    private Dictionary<string, ConnectionData> sessions = new Dictionary<string, ConnectionData>();

    /// <summary>
    /// Component that allows the WebRTC communication.
    /// </summary>
    private WebRTCReceiver receiver;
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

            listener = new UdpClient();
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            //listener.JoinMulticastGroup(IPAddress.Parse(MulticastGroup), IPAddress.Parse(ipAddress));
            listener.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse(MulticastGroup), IPAddress.Any));

            listenThread = new Thread(BroadcastListenLoop) { IsBackground = true, Name = "StreamManager TCP discovering loop" };
            listenThread.Start();
        }
    }

    /// <summary>
    /// Session discovery thread
    /// </summary>
    private void BroadcastListenLoop()
    {
        if (debug)
            UnityMainThreadDispatcher.Instance().Enqueue(()
                => Debug.Log($"[StreamManager] Launched loop - {running} / {connected}"));

        while (running && !connected)
        {
            try
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);

                if (string.IsNullOrEmpty(message))
                {
                    Debug.LogWarning("[StreamManager] Ignoring non-valid UDP package");
                    continue;
                }

                ConnectionData decodedData = JsonUtility.FromJson<ConnectionData>(message);
                if (decodedData.connType != ConnectionEvent.BROADCAST)
                    continue;

                if (debug) UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log("[StreamManager] Session found via BROADCAST"));
                StoreSession(decodedData);
            }
            catch (SocketException)
            {
                break; // Socket closed
            }
            catch (Exception e)
            {
                if (running)
                {
                    Debug.LogWarning($"[StreamManager] TCP session discovery thread error: {e.Message}");
                    CloseConnection();
                }
            }
        }
    }

    /// <summary>
    /// Tries to establish a connection to a selected session (host)
    /// </summary>
    /// <param name="data"></param>
    public void ConnectViaTCP(ConnectionData data)
    {
        if (currentState == ClientConnectionState.Connecting || currentState == ClientConnectionState.Connected)
        {
            Debug.LogWarning("[StreamManager] ConnectViaTCP ignored because other is already in progress");
            return; // <-- falta este return, ahora mismo sigue ejecutando igual
        }

        currentTransport = ConnectionTransport.TCP;
        intentionalDisconnect = false;
        currentState = ClientConnectionState.Connecting;
        hostIP = data.ipAddress;
        hostPort = data.port;
        UIManager.Instance.ConnectionAttemp();

        new Thread(() => TryTCPConnection(data)) { IsBackground = true, Name = "TCP Connect" }.Start();
    }

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

                SendHandshake();
                if (debug) Debug.Log($"[StreamManager] Handshake sent to {hostIP}:{hostPort}");

                UnityMainThreadDispatcher.Instance().Enqueue(OnConnectionStarted);
                return;
            }
            catch (SocketException se)
            {
                Debug.LogWarning($"TCP connect attempt {attempt}/{tcpConnectMaxRetries} failed: {se.Message}");
                CleanupSocket();
                if (attempt < tcpConnectMaxRetries) Thread.Sleep(tcpConnectRetryDelayMs);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreamManager] Unexpected error during TCP connect: {ex}");
                CleanupSocket();
                break; // no tiene sentido reintentar un bug de programación
            }
        }

        Debug.LogError($"[StreamManager] Could not connect via TCP to {hostIP}:{hostPort} after {tcpConnectMaxRetries} attempts.");
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            RemoveSession(data);
            CloseConnection();
        });
    }
    #endregion

    #region ADB
    /// <summary>
    ///  Starts a connection attemp via ADB
    /// </summary>
    public void ConnectViaADB()
    {
        if (currentState == ClientConnectionState.Connecting || currentState == ClientConnectionState.Connected)
        {
            Debug.LogWarning("[StreamManager] ConnectViaADB ignored, a connection attempt is already in progress");
            return;
        }

        intentionalDisconnect = false;
        currentTransport = ConnectionTransport.ADB;
        currentState = ClientConnectionState.Connecting;
        UIManager.Instance.ConnectionAttemp();

        new Thread(TryADBConnection) { IsBackground = true, Name = "ADB Connect" }.Start();
    }

    /// <summary>
    /// Tries to establish a connection to a connection via ADB (with an attemps limit)
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
                SendHandshake();

                connected = true;
                currentState = ClientConnectionState.Connected;
                if (debug) Debug.Log("[StreamManager] Connected to host via ADB (USB).");
                // Al conectar por ADB nos vamos directamente a la escena main

                readThread = new Thread(ReadLoop) { IsBackground = true, Name = "StreamManager ADB Readloop" };
                readThread.Start();
                return;
            }
            catch (SocketException se)
            {
                Debug.LogWarning($"TCP connect attempt {attempt}/{tcpConnectMaxRetries} failed: {se.Message}");
                CleanupSocket();
                if (attempt < tcpConnectMaxRetries) Thread.Sleep(tcpConnectRetryDelayMs);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreamManager] Unexpected error during TCP connect: {ex}");
                CleanupSocket();
                break; // no tiene sentido reintentar un bug de programación
            }
        }

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            Debug.LogError("Could not connect over ADB. Check the cable, USB debugging, and that the host has 'adb reverse' set up.");
            UIManager.Instance.ConnectionFailed(ipAddress);
            CloseConnection();
        });
    }
    #endregion

    #region Shared Methods
    /// <summary>
    /// Sends 'Handshake' message to host
    /// </summary>
    public void SendHandshake()
    {
        var currentStream = GetStreamSafe();
        if (currentStream == null)
        {
            Debug.LogWarning("[StreamManager] SendHandshake ignored, stream is not available");
            return;
        }

        string json = JsonUtility.ToJson(ConnectionData.ForHandshake(ipAddress, clientType));
        NetworkUtils.WriteFramedMessage(currentStream, json);
    }

    /// <summary>
    /// What to do once a connection is established
    /// </summary>
    public void OnConnectionStarted()
    {
        connected = true;
        // Create and configure the receiver
        GameObject client = new GameObject();
        client.transform.position = Vector3.zero;
        InputManager input = client.AddComponent<InputManager>();
        receiver = client.AddComponent<WebRTCReceiver>();
        receiver.SetUpAndInitialize(UIManager.Instance.GetDisplayTarget(), SendMessage);
        input.SetReceiver(receiver);

        readThread = new Thread(ReadLoop) { IsBackground = true };
        readThread.Start();
        CleanupListener();
    }

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
                if (!NetworkUtils.TryReadFramedMessage(currentStream, out string json)) break;

                var msg = JsonUtility.FromJson<SignalingMessage>(json);
                UnityMainThreadDispatcher.Instance()?.Enqueue(() => HandleMessage(msg));
            }
            catch (ObjectDisposedException)
            {
                break; // el stream se cerró desde otro hilo mientras leíamos, salida normal
            }
            catch (Exception e)
            {
                if (running) Debug.LogWarning($"[StreamManager] {e.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// Process messages for WebRTC configuration
    /// </summary>
    /// <param name="msg"></param>
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
    /// <param name="msg"></param>
    private void SendMessage(SignalingMessage msg)
    {
        var currentStream = GetStreamSafe();
        if (currentStream == null)
        {
            Debug.LogWarning("[StreamManager] SendMessage ignored, stream is not available");
            return;
        }

        try
        {
            NetworkUtils.WriteFramedMessage(currentStream, JsonUtility.ToJson(msg));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[StreamManager] SendMessage failed: {e.Message}");
        }
    }
    #endregion

    #region Session management
    /// <summary>
    /// Adds the new session discovered and creates an object on scene via UIManager
    /// </summary>
    /// <param name="data"></param>
    private void StoreSession(ConnectionData data)
    {
        if (sessions.TryGetValue(data.ipAddress, out ConnectionData duplicatedSession)) return;

        sessions.Add(data.ipAddress, data);
        UIManager.Instance.AddNewSessionUI(data);
    }

    /// <summary>
    /// Removes a session that is no longer available
    /// </summary>
    /// <param name="data"></param>
    private void RemoveSession(ConnectionData data)
    {
        sessions.Remove(data.ipAddress);
    }
    #endregion

    #region CleanUp
    public void Disconnect()
    {
        intentionalDisconnect = true;
        CloseConnection();
    }

    private void OnConnectionLost(string reason)
    {
        intentionalDisconnect = false;
        CloseConnection();
    }

    /// <summary>
    /// Closes sockets/threads and notifies listeners. Safe to call even if already disconnected.
    /// </summary>
    private void CloseConnection()
    {
        bool wasActive = connected;
        connected = false;
        currentState = ClientConnectionState.Disconnected;
        currentTransport = ConnectionTransport.NONE;

        if (wasActive && intentionalDisconnect && GetStreamSafe() != null)
        {
            try { SendMessage(new SignalingMessage(ConnectionEvent.DISCONNECT, null)); } catch { }
        }

        CleanupSocket();

        if (wasActive) Destroy(receiver.gameObject);
    }

    /// <summary>
    /// Closes all structures after disconnection
    /// </summary>
    private void CleanupSocket()
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

    private void CleanupListener()
    {
        try { listener?.Close(); } catch { }
        listener = null;
        listenThread?.Join(500);
    }
    #endregion

    #region Monobehaviour
    private void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        running = false;
        connected = false;
        ipAddress = NetworkUtils.GetIP();
        clientType = ClientType.NONE;

        StartCoroutine(WebRTC.Update());
    }

    private void Start()
    {
        StartListening();
    }

    private void Update()
    {
        if (debug)
        {
            debug = false;
            StoreSession(ConnectionData.ForBroadcast("199.10.22.12", 7728, "TEst", 2765));
        }
    }

    private void OnDestroy()
    {
        running = false;
        CloseConnection();
        CleanupListener();
    }
    #endregion
}
