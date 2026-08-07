using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

public delegate GameObject CreateClient();

public class StreamManager : MonoBehaviour
{
    #region Variables
    public static StreamManager Instance { get; private set; }

    [SerializeField]
    private bool debug = false;

    public ClientConnectionState currentState { get; private set; } = ClientConnectionState.Disconnected;
    public ConnectionTransport currentTransport { get; private set; } = ConnectionTransport.NONE;
    [SerializeField] private bool autoReconnect = true;
    [SerializeField] private int reconnectDelayMs = 2000;
    [SerializeField] private int maxReconnectAttempts = 5; 

    [SerializeField] private int discoveryTimeoutMs = 8000;
    [SerializeField] private int tcpConnectMaxRetries = 5;
    [SerializeField] private int tcpConnectRetryDelayMs = 1500;

    [SerializeField] private int adbRemotePort = 7778;
    [SerializeField] private int adbConnectMaxRetries = 6;
    [SerializeField] private int adbConnectRetryDelayMs = 1000;

    private volatile bool intentionalDisconnect;
    private volatile bool connectionActive;

    /// <summary>
    /// Wether the server is running or not.
    /// </summary>
    private bool running;

    /// <summary>
    /// Wether this machine is connected to a host or not.
    /// </summary>
    private bool connected;

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


    private Dictionary<string, ConnectionData> sessions;

    private CreateClient clientCallback;

    /// <summary>
    /// Component that allows the WebRTC communication.
    /// </summary>
    private WebRTCReceiver receiver;
    #endregion

    #region TCP
    public void StartListening()
    {
        if (!connected)
        {
            if(debug) Debug.Log("[StreamManager] Launching listen loop.");
            running = true;

            listener = new UdpClient();
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            //listener.JoinMulticastGroup(IPAddress.Parse(MulticastGroup), IPAddress.Parse(ipAddress));
            listener.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse(MulticastGroup), IPAddress.Any));

            listenThread = new Thread(BroadcastListenLoop) { IsBackground = true };
            listenThread.Start();
        }
    }

    private void BroadcastListenLoop()
    {
        if (debug)
            UnityMainThreadDispatcher.Instance().Enqueue(()
                => Debug.Log($"[ConnManager] Launched loop - {running} / {connected}"));

        while (running && !connected)
        {
            try
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);

                if (string.IsNullOrEmpty(message))
                {
                    Debug.LogWarning("[StreamManager] Empty UDP package, ignoring.");
                    continue;
                }

                ConnectionData decodedData = JsonUtility.FromJson<ConnectionData>(message);
                if (decodedData.connType != ConnectionEvent.BROADCAST)
                    continue;

                if (debug) UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log("[StreamManager] Session found via BROADCAST"));
                // Antes se conectaba de una, ahora guardamos sesion y mostramos por UI (no se hace handshake hasta que se clica)
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
                    Debug.LogWarning($"[StreamManager] Broadcast thread error: {e.Message}");
                    //HandleDisconnection();
                }
            }
        }
    }
    #endregion

    #region Shared Methods
    public void TryConnection(ConnectionData data)
    {
        hostIP = data.ipAddress;
        hostPort = data.port;

        if (debug) Debug.Log($"[StreamManager] Host found: {hostIP} — answering…");

        try
        {
            string json = JsonUtility.ToJson(ConnectionData.ForHandshake(ipAddress, clientType));
            byte[] responseData = Encoding.UTF8.GetBytes(json);
            byte[] header = BitConverter.GetBytes(responseData.Length);

            hostConnection = new TcpClient();
            hostConnection.Connect(hostIP, hostPort);
            stream = hostConnection.GetStream();
            NetworkUtils.WriteFramedMessage(stream, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[StreamManager] TCP connection error: {e.Message}");
            RemoveSession(data);
        }

        if (debug) Debug.Log($"[StreamManager] Handshake sent to {hostIP}:{hostPort}");
        //Antes se llamaba a ClientSignalingHandler.Instance?.StartSession(tcp, stream) y Old_UIManager.Instance.OnConnectionStarted(hostIP)
        OnConnectionStarted();
    }

    public void OnConnectionStarted()
    {
        connected = true;
        // Create and configure the receiver
        GameObject client = clientCallback();
        receiver = client.AddComponent<WebRTCReceiver>();
        receiver.SetUpAndInitialize(null, SendMessage);

        // NOTE: maybe this has to be changed or documented, because it assumes that the same go
        // has at least both ClienSignalingHandler and InputManager attached to it.
        //GetComponent<ClientInputManager>().SetReceiver(receiver);

        readThread = new Thread(ReadLoop) { IsBackground = true };
        readThread.Start();
    }

    private void ReadLoop()
    {
        while (running)
        {
            try
            {
                if (!NetworkUtils.TryReadFramedMessage(stream, out string json)) break;

                var msg = JsonUtility.FromJson<SignalingMessage>(json);

                // Despachar al main thread
                UnityMainThreadDispatcher.Instance()?.Enqueue(() => HandleMessage(msg));
            }
            catch (Exception e)
            {
                if (running) Debug.LogWarning($"[StreamManager] {e.Message}");
                break;
            }
        }
    }

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
    #endregion

    private void StoreSession(ConnectionData data)
    {
        if (sessions.TryGetValue(data.ipAddress, out ConnectionData duplicatedSession)) return;

        sessions.Add(data.ipAddress, data);
        // Meter nueva sesion en displayer
    }

    private void RemoveSession(ConnectionData data)
    {
        sessions.Remove(data.ipAddress);
        // Retirar sesion del displayer
    }

    private void SendMessage(SignalingMessage msg)
    {
        NetworkUtils.WriteFramedMessage(stream, JsonUtility.ToJson(msg));
    }

    #region CleanUp
    private void HandleConnectionLost(string reason)
    {
        if (!connectionActive) return; // already being handled by another path

        ConnectionTransport transportAtDrop = currentTransport;
        CloseConnection(reason);

        if (!intentionalDisconnect && autoReconnect)
            AttemptReconnect(transportAtDrop);
    }

    private void AttemptReconnect(ConnectionTransport transport)
    {
        currentState = ClientConnectionState.Reconnecting;
        new Thread(() => ReconnectRoutine(transport)) { IsBackground = true, Name = "Reconnect" }.Start();
    }

    private void ReconnectRoutine(ConnectionTransport transport)
    {
        for (int attempt = 1; attempt <= maxReconnectAttempts; attempt++)
        {
            if (intentionalDisconnect) return;

            Debug.Log($"Reconnect attempt {attempt}/{maxReconnectAttempts} ({transport})...");
            Thread.Sleep(reconnectDelayMs);

            if (transport == ConnectionTransport.ADB)
            {
                //EstablishADBConnection();
            }
            else
            {
                // Re-discover instead of reusing the last known IP: network conditions
                // (or the host's IP itself) may have changed while we were disconnected.
                
            }

            if (currentState == ClientConnectionState.Connected) return;
        }

        Debug.LogError("Reconnection failed after several attempts.");
        currentState = ClientConnectionState.Disconnected;
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
            try { SendMessage(new SignalingMessage(ConnectionEvent.DISCONNECT, null)); } catch { }
        }

        CleanupSocket();

        if (wasActive)
        {
            currentState = ClientConnectionState.Disconnected;
            if (debug) Debug.Log($"Disconnected: {reason}");
            //Que hacemos al desconectar-> escena de menu principal
        }
    }

    private void CleanupSocket()
    {
        try { stream?.Close(); } catch { }
        try { hostConnection?.Close(); } catch { }
        stream = null;
        hostConnection = null;
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
    }

    private void OnDestroy()
    {
        CloseConnection("Object destroyed");
    }
    #endregion
}
