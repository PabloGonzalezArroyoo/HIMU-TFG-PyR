using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

public delegate RenderTexture TextureAssignmentCallback(ClientData client, params object[] args);

public delegate void CreateClient(ClientData client);

public class StreamManager : MonoBehaviour
{

    #region Variables

    /// <summary>
    /// Instance of StreamManager (Singleton)
    /// </summary>
    public static StreamManager Instance { get; private set; }

    /// <summary>
    /// All currently connected clients, keyed by their clientID (GUID for TCP clients,
    /// session key for browser clients).
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
    /// Handles phones connected by USB cable through ADB.
    /// </summary>
    private ADBConnectionServer adbServer;

    /// <summary>
    /// Flag that allows WebSocket connections (browser)
    /// </summary>
    [SerializeField]
    private bool acceptWebSocketConnection = true;
    private bool webSocketConnectionOn = false;

    /// <summary>
    /// Flag that allows TCP connections (devices)
    /// </summary>
    [SerializeField]
    private bool acceptTCPConnection = true;
    private bool tcpConnectionOn = false;

    /// <summary>
    /// Flag that allows USB connections (cabled devices)
    /// </summary>
    [SerializeField]
    private bool acceptADBConnection = true;
    private bool adbConnectionOn = false;

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

    private TextureAssignmentCallback browserTextureCallback;
    private TextureAssignmentCallback tcpTextureCallback;
    private TextureAssignmentCallback adbTextureCallback;

    private CreateClient browserClientCallback;
    private CreateClient tcpClientCallback;
    private CreateClient adbClientCallback;
    #endregion

    #region SharedMethods

    public void RemovePeer(string clientID)
    {
        if (!clients.TryRemove(clientID, out var data))
        {
            Debug.LogWarning($"[StreamManager] Tried to remove unknown peer: {clientID}");
            return;
        }

        if (data.webRtcPeer != null)
            Destroy(data.webRtcPeer.gameObject);

        Debug.Log($"[StreamManager] Destroyed {data.transport} peer: {clientID}");
    }

    #endregion

    #region FlaggedMethods

    /// <summary>
    /// Starts or stops the TCP signaling server wether its checkbox is checked
    /// </summary>
    public void FlagSignalingServer()
    {
        if (!acceptTCPConnection) return;
        if (tcpConnectionOn)
        {
            UnityEngine.Debug.Log("[StreamManager] Stopping TCP signaling server.");
            signalingServer.StopServer();
            tcpConnectionOn = false;
        }
        else
        {
            UnityEngine.Debug.Log("[StreamManager] Launching TCP signaling server.");
            signalingServer.StartServer();
            tcpConnectionOn = true;
        }
    }

    /// <summary>
    /// Starts or stops the WebSocket server wether its checkbox is checked
    /// </summary>
    public async void FlagWebSocketServer()
    {
        if (!acceptWebSocketConnection) return;
        if (webSocketConnectionOn)
        {
            UnityEngine.Debug.Log("[StreamManager] Stopping WebSocket server (Node).");
            await webSocketServer.DisconnectToNode();
            webSocketServer.StopServer();
            webSocketConnectionOn = false;
        }
        else
        {
            UnityEngine.Debug.Log("[StreamManager] Launching WebSocket server (Node).");
            webSocketServer.LaunchServer();
            webSocketServer.ConnectToNode();
            webSocketConnectionOn = true;
        }
    }

    /// <summary>
    /// Starts or stops the WebSocket server wether its checkbox is checked
    /// </summary>
    public void FlagADBConnection()
    {
        if (!acceptADBConnection) return;
        if (adbConnectionOn)
        {
            UnityEngine.Debug.Log("[StreamManager] Stopping ADB connection.");
            adbServer.StopServer();
            acceptADBConnection = false;
        }
        else
        {
            UnityEngine.Debug.Log("[StreamManager] Launching ADB connection.");
            adbServer.StartServer();
            adbConnectionOn = true;
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
        if (!clients.TryAdd(clientID, client)) return;

        GameObject go = new GameObject($"{client.type.ToString()}-Bsw-Peer_{client.identifier}");
        WebRTCPeer peer = go.AddComponent<WebRTCPeer>();
        peer.Initialize(clientID, streamingTexture, msg => _ = webSocketServer.SendToNode(msg, clientID));
        StartCoroutine(peer.CreateOffer());
        clients[clientID].webRtcPeer = peer;
        Debug.Log($"[StreamManager] Created browser peer: {client.identifier} (id: {clientID})");
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
        if (!clients.TryAdd(clientID, client)) return;

        // Create GameObject
        GameObject go = new GameObject($"{client.type.ToString()}-Dvc-Peer_{client.identifier}");
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

        Debug.Log($"[StreamManager] Created device peer: {client.identifier} (id: {clientID})");
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
            RemovePeer(clientID);
        }
    }

    #endregion

    #region ADB

    /// <summary>
    /// Creates the client object and completes the WebRTC connection exchange for a phone
    /// connected by USB. Identical to CreatePeerForClient except messages are routed through
    /// the adbServer (TCP tunnel via "adb reverse") instead of the signalingServer.
    /// </summary>
    public void CreatePeerForADBClient(ClientData client)
    {
        string clientID = client.clientID;
        if (!clients.TryAdd(clientID, client)) return;

        GameObject go = new GameObject($"{client.type.ToString()}-Adb-Peer_{client.identifier}");
        DontDestroyOnLoad(go);
        go.GetComponent<Transform>().position = Vector3.zero;
        Camera cam = go.AddComponent<Camera>();

        RenderTexture rt = new RenderTexture((int)streamFrameWidth, (int)streamFrameHeight, (int)streamFrameDepth, RenderTextureFormat.BGRA32);
        rt.enableRandomWrite = true;
        rt.useMipMap = false;
        rt.antiAliasing = 1;
        rt.Create();
        cam.targetTexture = rt;

        WebRTCPeer peer = go.AddComponent<WebRTCPeer>();
        peer.Initialize(clientID, rt, msg => adbServer.SendMessage(clientID, msg));
        clients[clientID].webRtcPeer = peer;
        StartCoroutine(peer.CreateOffer());

        Debug.Log($"[StreamManager] Created ADB peer: {client.identifier} (id: {clientID})");
    }

    #endregion

    #region Getters & Setters

    public RenderTexture GetStreamCamera()
    {
        return streamingTexture;
    }

    /// <summary>
    /// Sets the new streamed camera by pointing its rendered texture to the streaming texture.
    /// </summary>
    /// <param name="newCamera">New stream camera.</param>
    public void SetStreamCamera(Camera newCamera)
    {
        newCamera.targetTexture = streamingTexture;
    }

    /// <summary>
    /// Copies the current clients to the returned list.
    /// </summary>
    /// <returns>Copy of the clients list.</returns>
    public List<ClientData> GetClients()
    {
        List<ClientData> copy = new List<ClientData>();
        foreach (var client in clients)
            copy.Add(client.Value);

        return copy;
    }

    public string GetServerData()
    {
        return webSocketServer.GetNodeHost() + ":" + webSocketServer.GetBrowserPort().ToString();
    }

    public int GetADBClients()
    {
        return 0;
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

        streamingTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.BGRA32);
        streamingTexture.enableRandomWrite = true;
        streamingTexture.useMipMap = false;
        streamingTexture.antiAliasing = 1;
        streamingTexture.Create();
    }

    private void Start()
    {
        if (acceptWebSocketConnection) webSocketServer = gameObject.AddComponent<WebSocketServerRTC>();
        if (acceptTCPConnection) signalingServer = gameObject.AddComponent<SignalingServer>();
        if (acceptADBConnection) adbServer = gameObject.AddComponent<ADBConnectionServer>();
    }
    #endregion
}