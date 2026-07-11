using System;
using System.Collections.Concurrent;
using Unity.WebRTC;
using UnityEngine;

public class StreamManager : MonoBehaviour
{

    #region Variables

    /// <summary>
    /// Instance of StreamManager (Singleton)
    /// </summary>
    public static StreamManager Instance { get; private set; }

    /// <summary>
    /// All currently connected clients, keyed by their IP.
    /// ConcurrentDictionary is used instead of Dictionary + lock because multiple background
    /// threads (one per client) may add or remove entries simultaneously
    /// </summary>
    readonly ConcurrentDictionary<string, ClientData> clients = new ConcurrentDictionary<string, ClientData>();

    /// <summary>
    /// Main Camera of the game
    /// </summary>
    [SerializeField]
    private GameObject mainCamera;

    /// <summary>
    /// Texture streamed to other devices (physical or browser)
    /// </summary>
    private RenderTexture streamingTexture;

    /// <summary>
    /// Server that works through a WebSocket. It connects to an external siganling server.
    /// </summary>
    private WebSocketServerRTC webSocketServer;

    /// <summary>
    /// An embeded Signaling Server in the game.
    /// </summary>
    private SignalingServer signalingServer;

    /// <summary>
    /// Flag that allows WebSocket connections (browser)
    /// </summary>
    [SerializeField]
    private bool acceptWebSocketConnection = true;

    /// <summary>
    /// Flag that allows TCP connections (devices)
    /// </summary>
    [SerializeField]
    private bool acceptTCPConnection = true;

    /// <summary>
    /// Flag that allows USB connections (cabled devices)
    /// </summary>
    [SerializeField]
    private bool acceptUSBConnection = true;

    /// <summary>
    /// Frame's width
    /// </summary>
    [SerializeField]
    private uint streamFrameWidth = 1280;

    /// <summary>
    /// Frame's heigth
    /// </summary>
    [SerializeField]
    private uint streamFrameHeight = 720;

    /// <summary>
    /// Frame's depth
    /// </summary>
    [SerializeField]
    private uint streamFrameDepth = 24;

    #endregion

    #region SharedMethods

    /// <summary>
    /// Adds a client to the dictionary
    /// </summary>
    /// <param name="str">IP of the client</param>
    /// <param name="client">Client data</param>
    public bool addClient(string ip, ClientData client)
    {
        return clients.TryAdd(ip, client);
    }

    /// <summary>
    /// Removes a client form the dictionary
    /// </summary>
    /// <param name="str">IP of the client</param>
    public bool removeClient(string ip)
    {
        return clients.TryRemove(ip, out var client);
    }

    #endregion

    #region Flagged_Methods

    /// <summary>
    /// Starts or stops the TCP signaling server wether its checkbox is checked
    /// </summary>
    public void FlagSignalingServer()
    {
        if (acceptTCPConnection)
        {
            UnityEngine.Debug.Log("[StreamManager] Stopping TCP signaling server.");
            signalingServer.StopServer();
            acceptTCPConnection = false;
        }
        else
        {
            UnityEngine.Debug.Log("[StreamManager] Launching TCP signaling server.");
            signalingServer.StartServer();
            acceptTCPConnection = true;
        }
    }

    /// <summary>
    /// Starts or stops the WebSocket server wether its checkbox is checked
    /// </summary>
    public async void FlagWebSocketServer()
    {
        if (acceptWebSocketConnection)
        {
            UnityEngine.Debug.Log("[StreamManager] Stopping WebSocket server (Node).");
            await webSocketServer.DisconnectToNode();
            webSocketServer.StopServer();
            acceptWebSocketConnection = false;
        }
        else 
        {
            UnityEngine.Debug.Log("[StreamManager] Launching WebSocket server (Node).");
            webSocketServer.LaunchServer();
            webSocketServer.ConnectToNode();
            acceptWebSocketConnection = true;
        }
    }

    /// <summary>
    /// Starts or stops the WebSocket server wether its checkbox is checked
    /// </summary>
    public void FlagADBConnection()
    {
        if (acceptUSBConnection)
        {
            UnityEngine.Debug.Log("[StreamManager] Stopping ADB connection.");
            // TO-DO
            acceptUSBConnection = false;
        }
        else
        {
            UnityEngine.Debug.Log("[StreamManager] Launching ADB connection.");
            // TO-DO
            acceptUSBConnection = true;
        }
    }

    #endregion

    #region WebSocket

    /// <summary>
    /// Creacion de objeto en escena que representa un cliente Navegador
    /// </summary>
    /// <param name="client"></param>
    public void CreatePeerForBrowser(ClientData client)
    {
        // Si ese navegador ya esta conectado, se ignora
        string clientID = client.clientID;
        if (!addClient(clientID, client)) return;

        GameObject go = new GameObject($"{client.type.ToString()}-Bsw-Peer_{client.ipAddress}");
        WebRTCPeer peer = go.AddComponent<WebRTCPeer>();
        peer.Initialize(clientID, streamingTexture, msg => webSocketServer.SendToNode(msg, clientID));
        StartCoroutine(peer.CreateOffer());
        clients[clientID].webRtcPeer = peer;
        Debug.Log($"[StreamManager] Created browser peer: {client.ipAddress} (id: {clientID})");
    }

    /// <summary>
    /// Elimina lo creado para representar al cliente navegador
    /// </summary>
    /// <param name="clientID"></param>
    public void RemovePeerForBrowser(string clientID)
    {
        Destroy(clients[clientID].webRtcPeer.DestroyPeer());
        clients.TryRemove(clientID, out var data);
        Debug.Log($"[StreamManager] Destroyed browser peer: {clientID}");
    }

    #endregion

    #region TCP
    /// <summary>
    /// Creates the client object and completes the WebRTC connection exchange
    /// </summary>
    /// <param name="ip">IP of the client</param>
    public void CreatePeerForClient(ClientData client)
    {
        // Add client to the dictionary
        string clientID = client.clientID;
        if (!addClient(clientID, client)) return;

        // Create GameObject
        GameObject go = new GameObject($"{client.type.ToString()}-Dvc-Peer_{client.ipAddress}");
        DontDestroyOnLoad(go);
        go.GetComponent<Transform>().position = Vector3.zero;
        Camera cam = go.AddComponent<Camera>();

        RenderTexture rt;
        rt = new RenderTexture((int)streamFrameWidth, (int)streamFrameHeight, (int)streamFrameDepth, RenderTextureFormat.BGRA32);
        rt.enableRandomWrite = true;
        rt.useMipMap = false;
        rt.antiAliasing = 1;
        rt.Create();
        cam.targetTexture = rt;

        // Create RTC connection Peer
        WebRTCPeer peer = go.AddComponent<WebRTCPeer>();
        peer.Initialize(clientID, rt, msg => signalingServer.SendMessage(clientID, msg));
        clients[clientID].webRtcPeer = peer;
        StartCoroutine(peer.CreateOffer());

        Debug.Log($"[StreamManager] Created device peer: {client.ipAddress} (id: {clientID})");
    }

    /// <summary>
    /// Porcesses the signaling message from the server
    /// </summary>
    /// <param name="clientID"></param>
    /// <param name="msg"></param>
    public void HandleIncomingSignaling(string clientID, SignalingMessage msg)
    {
        if (!clients.TryGetValue(clientID, out var peer)) return;

        if (msg.type == ConnectionEvent.ICE)
        {
            IceCandidateData data = JsonUtility.FromJson<IceCandidateData>(msg.body);
            RTCIceCandidateInit init = new RTCIceCandidateInit
            {
                candidate = data.candidate,
                sdpMid = data.sdpMid,
                sdpMLineIndex = data.sdpMLineIndex
            };
            peer.webRtcPeer.AddIceCandidate(init);
        }
        else if (msg.type == ConnectionEvent.SDP)
        {
            SessionDescriptionData data = JsonUtility.FromJson<SessionDescriptionData>(msg.body);
            RTCSessionDescription answer = data.ToRTCDesc();
            StartCoroutine(peer.webRtcPeer.SetRemoteAnswer(answer));
        }
        else if (msg.type == ConnectionEvent.DISCONNECT)
        {
            Destroy(peer.webRtcPeer.gameObject);
            removeClient(clientID);
            Debug.Log($"[StreamManager] Removed peer: {peer.ipAddress} (id: {clientID})");
        }
    }

    public void RemovePeerForClient(string clientID)
    {
        Destroy(clients[clientID].webRtcPeer.DestroyPeer());
        clients.TryRemove(clientID, out var data);
        Debug.Log($"[StreamManager] Destroyed client peer: {clientID}");
    }
    #endregion

    #region Getters & Setters

    /// <summary>
    /// Sets the new streamed camera by pointing its rendered texture to the streaming texture.
    /// </summary>
    /// <param name="newCamera">New stream camera.</param>
    public void SetStreamCamera(Camera newCamera)
    {
        newCamera.targetTexture = streamingTexture;
    }

    #endregion

    #region Monobehaviour
    void Awake()
    {
        if (Instance) { DestroyImmediate(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        StartCoroutine(WebRTC.Update());
        NetworkUtils.GetIP();
    }

    private void Start()
    {
        webSocketServer = gameObject.AddComponent<WebSocketServerRTC>();
        signalingServer = gameObject.AddComponent<SignalingServer>();
    }
    #endregion
}
