using NUnit.Framework.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

public class SessionListenerComponent : MonoBehaviour
{
    [SerializeField] ConnectionUIManager uiManager;
    [SerializeField] string IP_Server = "192.168.0.5";
    [SerializeField] int PORT_SERVER = 8080;
    [SerializeField] bool nodeConnections = true;
    [SerializeField] bool p2pConnections = true;
    private UdpClient listener;
    private Thread listenThread;
    private bool running = false;
    private int listenPort = 8053;
    ClientWebSocket ws; 
    [SerializeField]
    private GameObject VRCameraObject;

    /// <summary>
    /// Mapa de conexiones de navegador
    /// </summary>
    Dictionary<int, WebRTCReceiver> browserPeers = new Dictionary<int, WebRTCReceiver>();

    public void StartBroadcast()
    {
        Debug.Log("Lanzando listen loop");

        listener = new UdpClient(listenPort);
        // Empezar a escuchar mensajes Broadcast para encontrar sesiones
        listenThread = new Thread(BroadcastListenLoop) { IsBackground = true };
        listenThread.Start();
        // Buscar conexion a un posible servidor de NODE

    }

    private void BroadcastListenLoop()
    {
        while (running)
        {
            try
            {
                Debug.Log("Escuchando");
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);

                if (string.IsNullOrEmpty(message))
                {
                    UnityEngine.Debug.LogWarning("[UDP] Paquete vacío recibido, ignorando.");
                    continue;
                }

                ConnectionData decodedData = JsonUtility.FromJson<ConnectionData>(message);

                if (decodedData.connType != ConnectionEvent.BROADCAST)
                    continue;

                uiManager.AddNewSessionUI(decodedData);
            }
            catch (SocketException)
            {
                break; // Socket cerrado
            }
            catch (Exception e)
            {
                if (running)
                {
                    Debug.LogWarning($"[Client] Error en hilo de broadcast: {e.Message}");
                    //HandleDisconnection();
                }
            }
        }
    }

    private void ConnectToNodeServer()
    {

    }

    // Inicia la conexion al servidor de Node
    public async void ConnectToNode()
    {
        ws = new ClientWebSocket();
        Uri uri = new Uri($"ws://{IP_Server}:{PORT_SERVER}?type=unity");

        try
        {
            await ws.ConnectAsync(uri, CancellationToken.None);
            Debug.Log($"[StreamManager] Conectado a Node: {uri}");
            _ = ReceiveLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StreamManager] Error conectando a Node: {ex.Message}");
        }
    }

    async Task ReceiveLoop()
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        while (ws.State == WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                string json = sb.ToString();
                sb.Clear();

                UnityMainThreadDispatcher.Instance().Enqueue(() => HandleIncoming(json));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StreamManager] ReceiveLoop: {ex.Message}");
                break;
            }
        }
    }

    // Manejo de informacion recibida del servidor de Node
    void HandleIncoming(string rawJson)
    {
        WSBaseMessage baseMsg = JsonUtility.FromJson<WSBaseMessage>(rawJson);

        if (baseMsg.type == 99) // newClient
        {
            WSNewClientMessage newClient = JsonUtility.FromJson<WSNewClientMessage>(rawJson);
            //CreatePeerForBrowser(newClient.clientId);
        }
        else // SDP o ICE de un browser existente
        {
            WSTaggedMessage taggedMsg = JsonUtility.FromJson<WSTaggedMessage>(rawJson);
            ProcessSignaling(taggedMsg.clientId, taggedMsg.type, taggedMsg.body);
        }
    }

    // Manejo de mensajes de conexion
    void ProcessSignaling(int clientId, int type, string body)
    {
        if (!browserPeers.TryGetValue(clientId, out var peer)) return;

        if (type == (int)ConnectionEvent.ICE)
        {
            IceCandidateData data = JsonUtility.FromJson<IceCandidateData>(body);
            peer.AddIceCandidate(new RTCIceCandidateInit
            {
                candidate = data.candidate,
                sdpMid = data.sdpMid,
                sdpMLineIndex = data.sdpMLineIndex
            });
        }
        else if (type == (int)ConnectionEvent.SDP)
        {
            SessionDescriptionData data = JsonUtility.FromJson<SessionDescriptionData>(body);
            //StartCoroutine(peer.SetRemoteAnswer(data.ToRTCDesc()));
        }
        else if (type == (int)ConnectionEvent.DISCONNECT)
        {
            Destroy(peer.gameObject);
            browserPeers.Remove(clientId);
            Debug.Log($"[StreamManager] Peer eliminado para browser {clientId}");
        }
    }

    // Envia informacion al servidor de node
    async void SendToNode(SignalingMessage msg, int clientId)
    {
        if (ws?.State != WebSocketState.Open) return;
        string escapedBody = msg.body.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string json = $"{{\"type\":{(int)msg.type},\"body\":\"{escapedBody}\",\"clientId\":{clientId}}}";
        byte[] data = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(data),
            WebSocketMessageType.Text, true, CancellationToken.None);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        running = true;
        StartBroadcast();
    }

    private void OnDestroy()
    {
        running = false;
        listenThread.Abort();
        listenThread = null;
        listener.Close();
    }
}
