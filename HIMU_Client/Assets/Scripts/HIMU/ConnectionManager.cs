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
    public static ConnectionManager Instance { get; private set; }

    [SerializeField]
    private bool debug = false;

    public ClientConnectionState currentState { get; private set; } = ClientConnectionState.Disconnected;
    public ClientConnectionType currentTransport { get; private set; } = ClientConnectionType.NONE;

    [SerializeField] private int adbRemotePort = 7778;
    [SerializeField] private int adbConnectMaxRetries = 5;
    [SerializeField] private int adbConnectRetryDelayMs = 1500;

    [SerializeField] private float tunnelCheckIntervalSeconds = 2f;
    [SerializeField] private int tunnelProbeTimeoutMs = 500;
    public bool tunnelEstablished { get; private set; }
    private Thread tunnelWatcherThread;
    private volatile bool tunnelWatcherRunning;

    [SerializeField] private int tcpConnectMaxRetries = 5;
    [SerializeField] private int tcpConnectRetryDelayMs = 1500;
    [SerializeField] private int tcpConnectTimeoutMs = 1500;

    [SerializeField] private int handshakeTimeoutMs = 3000;

    private volatile bool intentionalDisconnect;

    /// <summary>
    /// Wether the server is running or not.
    /// </summary>
    private bool running;

    /// <summary>
    /// Wether this machine is connected to a host or not.
    /// </summary>
    public bool connected { get; private set; } = false;

    private volatile bool handshaking;

    /// <summary>
    /// What type of client this device is (STREAM or PLAYER, NONE = non existent device).
    /// </summary>
    [SerializeField]
    private ClientConnectionType clientType;

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
    private HIMUReceiver receiver = null;

    [SerializeField] private GameObject clientPrefab = null;
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

        currentTransport = ClientConnectionType.TCP;
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
                if (!Handshake(stream))
                {
                    CleanupSocket();
                    if (attempt < tcpConnectMaxRetries) Thread.Sleep(tcpConnectRetryDelayMs);
                    continue;
                }

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
                break;
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
        currentTransport = ClientConnectionType.ADB;
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
                
                if(!Handshake(stream, expectedHostPort: adbRemotePort))
                {
                    CleanupSocket();
                    if (attempt < adbConnectMaxRetries) Thread.Sleep(adbConnectRetryDelayMs);
                    continue;
                }

                connected = true;
                currentState = ClientConnectionState.Connected;
                if (debug) Debug.Log("[StreamManager] Connected to host via ADB (USB).");

                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    OnConnectionStarted();
                    UIManager.Instance.ConnectionSuccessful();
                });
                return;
            }
            catch (SocketException se)
            {
                Debug.LogWarning($"TCP connect attempt {attempt}/{adbConnectMaxRetries} failed: {se.Message}");
                CleanupSocket();
                if (attempt < adbConnectMaxRetries) Thread.Sleep(adbConnectRetryDelayMs);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreamManager] Unexpected error during TCP connect: {ex}");
                CleanupSocket();
                break;
            }
        }

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            Debug.LogError("Could not connect over ADB. Check the cable, USB debugging, and that the host has 'adb reverse' set up.");
            UIManager.Instance.ConnectionFailed(ipAddress);
            CloseConnection();
        });
    }

    /// <summary>
    /// Arranca el proceso secundario que comprueba periodicamente si el tunel 'adb reverse'
    /// esta activo. Es independiente del hilo de conexion real (TryADBConnection): nunca toca
    /// 'hostConnection' ni 'stream', asi que no interfiere con el intento de conexion en curso.
    /// </summary>
    private void StartTunnelWatcher()
    {
        StopTunnelWatcher(); // por si quedaba uno de un intento anterior sin cerrar

        tunnelEstablished = false;
        tunnelWatcherRunning = true;
        tunnelWatcherThread = new Thread(TunnelWatcherLoop) { IsBackground = true, Name = "ADB Tunnel Watcher" };
        tunnelWatcherThread.Start();
    }

    /// <summary>
    /// Detiene el proceso secundario de vigilancia del tunel, si estuviera activo.
    /// </summary>
    private void StopTunnelWatcher()
    {
        tunnelWatcherRunning = false;
        tunnelWatcherThread?.Join(500);
        tunnelWatcherThread = null;
    }

    /// <summary>
    /// Proceso secundario: cada 'tunnelCheckIntervalSeconds' comprueba si el tunel 'adb reverse'
    /// ya esta activo. Si lo detecta, activa 'tunnelEstablished' pero NO detiene el proceso -
    /// sigue sondeando por si el estado cambiase. Solo termina cuando el handshake se completa
    /// con exito (connected == true), cuando se agotan los reintentos, cuando hay una
    /// desconexion intencional, o cuando el propio hilo se manda a parar desde fuera.
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
                // El tunel desaparecio (p.ej. se desconecto el cable) justo mientras Handshake()
                // estaba bloqueado esperando el ACK. No tiene sentido dejar que agote el timeout
                // completo: forzamos el cierre del socket para que retorne ya y TryADBConnection
                // pueda reintentar antes.
                if (debug) UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    Debug.LogWarning("[StreamManager] Tunnel lost while waiting for handshake ACK, aborting attempt."));
                CleanupSocket();
            }

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        UIManager.Instance.UpdateADBButton(tunnelEstablished));

            // Esperamos en trozos pequeños para poder reaccionar rapido a que la conexion real
            // termine (con exito o no) sin tener que aguantar el intervalo completo.
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
    /// Abre una conexion TCP desechable a localhost:adbRemotePort solo para comprobar si hay
    /// algo escuchando ahi (es decir, si 'adb reverse' ya mapeo ese puerto al host). Se cierra
    /// inmediatamente sin enviar ningun handshake: la conexion real la gestiona por separado
    /// TryADBConnection.
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
    /// Sends 'Handshake' message to host
    /// </summary>
    private bool Handshake(NetworkStream netStream, int expectedHostPort = -1)
    {
        var currentStream = GetStreamSafe();
        if (currentStream == null)
        {
            Debug.LogWarning("[StreamManager] SendHandshake ignored, stream is not available");
            return false;
        }

        string json = JsonUtility.ToJson(ConnectionData.ForHandshake(ipAddress, clientType));
        NetworkUtils.WriteFramedMessage(currentStream, json);
        if (debug) Debug.Log($"[StreamManager] Handshake sent to {hostIP}:{hostPort}");

        handshaking = true;
        int previousTimeout = netStream.ReadTimeout;
        try
        {
            netStream.ReadTimeout = handshakeTimeoutMs;

            if (!NetworkUtils.TryReadFramedMessage(netStream, out string ackJson))
            {
                Debug.LogWarning("[StreamManager] Host closed the connection during handshake.");
                return false;
            }

            ConnectionData ack = JsonUtility.FromJson<ConnectionData>(ackJson);

            if (ack.connType != ConnectionEvent.HANDSHAKE)
            {
                Debug.LogWarning($"[StreamManager] Expected HANDSHAKE_ACK, got {ack.connType} instead.");
                return false;
            }

            if (expectedHostPort >= 0 && ack.port != expectedHostPort)
            {
                Debug.LogWarning($"[StreamManager] Host reported port {ack.port} but we connected " +
                    $"expecting {expectedHostPort}. Check that adbRemotePort/remotePort match between projects.");
            }

            if (debug) Debug.Log($"[StreamManager] Handshake received from host, connection established");
            return true;
        }
        catch (IOException)
        {
            Debug.LogWarning("[StreamManager] Timed out waiting for HANDSHAKE_ACK.");
            return false;
        }
        catch (ObjectDisposedException)
        {
            // El proceso secundario de vigilancia del tunel detecto que ya no estaba activo y
            // forzo el cierre del socket para no esperar aqui hasta agotar el timeout.
            Debug.LogWarning("[StreamManager] Handshake aborted: tunnel dropped while waiting for the ACK.");
            return false;
        }
        finally
        {
            handshaking = false;
            try { netStream.ReadTimeout = previousTimeout; }
            catch { /**/}
        }
    }

    /// <summary>
    /// What to do once a connection is established
    /// </summary>
    public void OnConnectionStarted()
    {
        connected = true;
        GameObject client = Instantiate(clientPrefab);
        client.transform.position = Vector3.zero;
        receiver = client.GetComponent<HIMUReceiver>();
        receiver.SetUpAndInitialize(SendMessage);

        readThread = new Thread(ReadLoop) { IsBackground = true };
        readThread.Start();
        StopTunnelWatcher();
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
                break; // el stream se cerró desde otro hilo mientras leíamos (nuestro propio Disconnect()), salida normal
            }
            catch (Exception e)
            {
                if (!intentionalDisconnect) UnityMainThreadDispatcher.Instance().Enqueue(OnConnectionLost);
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
        UnityMainThreadDispatcher.Instance().Enqueue(() => UIManager.Instance?.AddNewSessionUI(data));
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
        StopTunnelWatcher();
        CloseConnection();
    }

    private void OnConnectionLost()
    {
        intentionalDisconnect = false;
        CloseConnection();
        AppManager.Instance.ConnectionLost();
    }

    /// <summary>
    /// Closes sockets/threads and notifies listeners. Safe to call even if already disconnected.
    /// </summary>
    private void CloseConnection()
    {
        bool wasActive = connected;
        connected = false;
        currentState = ClientConnectionState.Disconnected;
        currentTransport = ClientConnectionType.NONE;

        if (wasActive && intentionalDisconnect && GetStreamSafe() != null)
        {
            try { SendMessage(new SignalingMessage(ConnectionEvent.DISCONNECT, null)); } catch { }
        }

        CleanupSocket();
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
