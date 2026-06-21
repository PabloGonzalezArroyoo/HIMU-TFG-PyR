using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.WebRTC;
using UnityEngine;

public class StreamManagerHost : MonoBehaviour
{

    #region Variables
    /// <summary>
    /// Instance of StreamManager (Singleton)
    /// </summary>
    public static StreamManagerHost Instance { get; private set; }

    /// <summary>
    /// The IP address of this device
    /// </summary>
    public static string ipAddress { get; private set; }

    /// <summary>
    /// All currently connected clients, keyed by their IP.
    /// ConcurrentDictionary is used instead of Dictionary + lock because multiple background
    /// threads (one per client) may add or remove entries simultaneously. All individual
    /// operations (TryAdd, TryRemove, TryGetValue) are atomic, and its enumerator works on
    /// a snapshot so Broadcast iteration is safe without an external lock.
    /// </summary>
    readonly ConcurrentDictionary<string, ClientData> clients = new ConcurrentDictionary<string, ClientData>();

    /// <summary>
    /// Main Camera of the game
    /// </summary>
    [SerializeField]
    private GameObject mainCamera;

    /// <summary>
    /// Textura de la camara creada para los browsers
    /// </summary>
    private RenderTexture streamTexture;

    /// <summary>
    /// Server that works through a WebSocket. It connects to an external siganling server.
    /// </summary>
    WebSocketServerRTC webSocketServer;

    /// <summary>
    /// An embeded Signaling Server in the game.
    /// </summary>
    SignalingServer signalingServer;

    //UIConnectionComponent connectionUI;

    /// <summary>
    /// Flag para permitir conexiones del tipo Navegador (WebSocket)
    /// </summary>
    [SerializeField] public bool acceptWebSocketConnection = true;

    /// <summary>
    /// Flag para permitir conexiones del tipo Navegador (TCP)
    /// </summary>
    [SerializeField] public bool acceptTCPConnection = true;

    /// <summary>
    /// Flag para permitir conexiones del tipo Navegador (WebSocket) - TO DO
    /// </summary>
    [SerializeField] public bool acceptUSBConnection = true;
    #endregion

    #region Flagged_Methods
    public void CreateSignalingServer()
    {
        signalingServer = gameObject.AddComponent<SignalingServer>();
    }

    private void CreateWebSocketServer()
    {
        webSocketServer = gameObject.AddComponent<WebSocketServerRTC>();
    }

    private void CreateUSBConnection()
    {
        // TO DO
    }
    #endregion

    #region SharedMethods

    private void CreateStreamCamera()
    {
        streamTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.BGRA32);
        streamTexture.enableRandomWrite = true;
        streamTexture.useMipMap = false;
        streamTexture.antiAliasing = 1;
        streamTexture.Create();

        GameObject streamGo = new GameObject($"StreamCamera");
        streamGo.transform.position = mainCamera.transform.position;
        streamGo.transform.rotation = mainCamera.transform.rotation;
        streamGo.transform.SetParent(mainCamera.transform);
        Camera cam = streamGo.AddComponent<Camera>();
        cam.targetTexture = streamTexture;
    }

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

    #region WebSocket
    /// <summary>
    /// Creacion de objeto en escena que representa un cliente Navegador
    /// </summary>
    /// <param name="client"></param>
    public void CreatePeerForBrowser(ClientData client)
    {
        // Si ese navegador ya esta conectado, se ignora
        string id = client.clientID;
        if (!addClient(id, client)) return;

        GameObject go = new GameObject($"{client.type.ToString()}-Peer_{id}");
        WebRTCPeer peer = go.AddComponent<WebRTCPeer>();
        peer.Initialize(id, streamTexture, msg => webSocketServer.SendToNode(msg, id));
        StartCoroutine(peer.CreateOffer());
        clients[id].webRtcPeer = peer;
        Debug.Log($"[StreamManager] Created browser peer: {id}");
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
        string ip = client.ipAddress;
        if (!addClient(ip, client)) return;

        // Create GameObject
        GameObject go = new GameObject($"{client.type.ToString()}-Peer_{ip}");
        go.GetComponent<Transform>().position = Vector3.zero;

        RenderTexture rt = null;
        // creamos todo lo del nuevo player: prefab?
        Camera cam = go.AddComponent<Camera>();
        cam.targetTexture = rt;

        // Create RTC connection Peer
        WebRTCPeer peer = go.AddComponent<WebRTCPeer>();
        peer.Initialize(ip, rt, msg => SendSignalingMessage(ip, msg));
        clients[ip].webRtcPeer = peer;
        StartCoroutine(peer.CreateOffer());
        Debug.Log($"[StreamManager] Created device peer: {ip}");

    }

    void SendSignalingMessage(string ip, SignalingMessage msg)
    {
        if (!clients.TryGetValue(ip, out var client)) return;
        string json = JsonUtility.ToJson(msg);
        byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
        byte[] header = System.BitConverter.GetBytes(data.Length);
        client.stream.Write(header, 0, 4);
        client.stream.Write(data, 0, data.Length);
        client.stream.Flush();
    }

    public void HandleIncomingSignaling(string fromIp, SignalingMessage msg)
    {
        if (!clients.TryGetValue(fromIp, out var peer)) return;

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
            removeClient(peer.ipAddress);
            Debug.Log($"[StreamManager] Removed peer: {peer.ipAddress}");
        }
    }

    public void RemovePeerForClient(string clientID)
    {
        Destroy(clients[clientID].webRtcPeer.DestroyPeer());
        clients.TryRemove(clientID, out var data);
        Debug.Log($"[StreamManager] Destroyed client peer: {clientID}");
    }
    #endregion

    #region Getters

    private void GetIpAddress()
    {
        ipAddress = "No disponible";
        try
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                // Excluir adaptadores virtuales (VirtualBox, VMware, Hyper-V, etc.)
                string name = ni.Name.ToLower();
                string desc = ni.Description.ToLower();
                if (name.Contains("virtual") || desc.Contains("virtual") ||
                    name.Contains("vmware") || desc.Contains("vmware") ||
                    name.Contains("vbox") || desc.Contains("vbox")) continue;

                IPInterfaceProperties props = ni.GetIPProperties();
                if (props.GatewayAddresses.Count == 0) continue;

                foreach (UnicastIPAddressInformation addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    ipAddress = addr.Address.ToString();
                    Debug.Log($"[Network] Adaptador: {ni.Name} � IP: {ipAddress}");
                    return;
                }
            }
#elif UNITY_ANDROID
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect(MulticastGroup, 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                ipAddress = endPoint.Address.ToString();
            }
            Debug.Log($"[Network] IP seleccionada: {ipAddress}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"[Network] Error obteniendo IP: {e}");
        }
    }

    public string GetIP()
    {
        return ipAddress;
    }

    #endregion


    #region Monobehaviour
    void Awake()
    {
        if (Instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        GetIpAddress();
        CreateStreamCamera();
    }
    #endregion
}
