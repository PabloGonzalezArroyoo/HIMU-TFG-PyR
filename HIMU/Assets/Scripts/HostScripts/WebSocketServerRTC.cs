using NUnit.Framework.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.LightTransport;

public class WebSocketServerRTC : MonoBehaviour
{

    // Lanzaremos tambien el bat desde aqui

    #region Variables
    /// <summary>
    /// Debe ser la IP del dispositivo que corre el servidor de Node (asumimos que es esta maquina misma)
    /// </summary>
    public string nodeHost = "192.168.1.45";
    [SerializeField] int nodePort = 8080;

    /// <summary>
    /// Socket de conexion al servidor de Node
    /// </summary>
    ClientWebSocket ws;

    /// <summary>
    /// Ruta del bat desde la carpeta raiz de la build o desde la carpeta raiz del proyecto
    /// </summary>
    [SerializeField] string batRelativePath = "start-server.bat";
    string batPath;

    /// <summary>
    /// Se usa para cancelar la task asincrona del ReceiveLoop
    /// </summary>
    private CancellationTokenSource _cts;

    /// <summary>
    /// Direccion del servidor de Node
    /// </summary>
    Uri nodeUri;

    /// <summary>
    /// Clientes (navegadores) conectados a traves del servidor de Node
    /// </summary>
    private readonly Dictionary<string, ClientData> connectedBrowsers = new Dictionary<string, ClientData>();
    #endregion

    #region Methods
    /// <summary>
    /// Lanza el servidor de Node ejecutando el archivo .bat
    /// </summary>
    public void LaunchServer()
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = batPath,
            WorkingDirectory = System.IO.Path.GetDirectoryName(batPath),
            UseShellExecute = true,
            CreateNoWindow = false
        };

        try
        {
            Process.Start(psi);
            UnityEngine.Debug.Log("[ServerLauncher] Script lanzado correctamente.");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[ServerLauncher] Error al lanzar el bat: {ex.Message}");
        }
    }

    /// <summary>
    /// Detiene el servidor de Node
    /// </summary>
    public void StopServer()
    {
        // Eliminar procesos de los comandos ejecutados en el bat (servidor de Node y servidor del html)
        foreach (var process in Process.GetProcessesByName("cmd"))
        {
            try
            {
                if (process.MainWindowTitle.Contains("Servidor Node") || process.MainWindowTitle.Contains("Servidor HTML"))
                {
                    process.Kill();
                    UnityEngine.Debug.Log($"[ServerLauncher] Cerrado: {process.MainWindowTitle}");
                }
            }
            catch { }
        }
        // Tambien hace falta matar el proceso node.exe interno
        foreach (var process in Process.GetProcessesByName("node"))
        {
            try
            {
                process.Kill();
                UnityEngine.Debug.Log($"[ServerLauncher] Proceso node (PID {process.Id}) cerrado.");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[ServerLauncher] No se pudo cerrar node: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Inicia la conexion al servidor de Node
    /// </summary>
    public async void ConnectToNode()
    {
        try
        {
            await ws.ConnectAsync(nodeUri, CancellationToken.None);
            UnityEngine.Debug.Log($"[StreamManager] Conectado a Node: {nodeUri}");
            _ = ReceiveLoop(_cts);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[StreamManager] Error conectando a Node: {ex.Message}");
        }
    }

    /// <summary>
    /// Detiene la conexion al servidor de Node
    /// </summary>
    public async void DisconnectToNode()
    {
        if (ws?.State == WebSocketState.Open && connectedBrowsers.Count > 0)
        {
            SignalingMessage byeMsg = new SignalingMessage(nodeHost, null, ConnectionEvent.DISCONNECT, null);

            // copia las claves para no modificar la colección mientras iteras
            foreach (string clientId in new List<string>(connectedBrowsers.Keys))
            { 
                await SendToNodeAsync(byeMsg, clientId);
                StreamManagerHost.Instance.RemovePeerForBrowser(clientId);
            }

        }

        _cts?.Cancel();
        if (ws?.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);

        connectedBrowsers.Clear();
    }

    /// <summary>
    /// Bucle de recepcion de mensajes WebSockets
    /// </summary>
    /// <param name="token">Token que actua como FLAG para poder cancelar el proceso</param>
    /// <returns></returns>
    async Task ReceiveLoop(CancellationTokenSource token)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        
        try
        {
            while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
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
                    UnityEngine.Debug.LogError($"[StreamManager] ReceiveLoop: {ex.Message}");
                    break;
                }
            }
        } catch (OperationCanceledException)
        {
            UnityEngine.Debug.LogError($"[StreamManager] ReceiveLoop: Se detuvo el proceso de recepcion de informacion del servidor de Node");
        }
    }
    
    /// <summary>
    /// Manejo de informacion recibida del servidor de Node
    /// </summary>
    /// <param name="rawJson"></param>
    void HandleIncoming(string rawJson)
    {
        WSBaseMessage baseMsg = JsonUtility.FromJson<WSBaseMessage>(rawJson);

        if (baseMsg.type == 99) // newClient
        {
            WSNewClientMessage newClient = JsonUtility.FromJson<WSNewClientMessage>(rawJson);
            string clientKey = newClient.clientId.ToString();

            ConnectionData connData = new ConnectionData(clientKey, nodePort, ConnectionEvent.HANDSHAKE, ClientType.STREAM);
            ClientData client = new ClientData(connData, null, clientKey); // <- ahora con 3 args

            connectedBrowsers[clientKey] = client;
            UnityEngine.Debug.Log($"[StreamManager] Browser registrado: {clientKey} (total: {connectedBrowsers.Count})");

            StreamManagerHost.Instance?.CreatePeerForBrowser(client);
        }
        else if (baseMsg.type == (int)ConnectionEvent.DISCONNECT) // un navegador se desconectó del lado de Node
        {
            WSTaggedMessage tagged = JsonUtility.FromJson<WSTaggedMessage>(rawJson);
            string clientKey = tagged.clientId.ToString();

            if (connectedBrowsers.Remove(clientKey))
                UnityEngine.Debug.Log($"[StreamManager] Browser eliminado del registro: {clientKey}");

            StreamManagerHost.Instance?.RemovePeerForBrowser(clientKey); 
        }
        else // SDP o ICE de un browser existente
        {
            WSTaggedMessage tagged = JsonUtility.FromJson<WSTaggedMessage>(rawJson);
            string clientKey = tagged.clientId.ToString();
            SignalingMessage sigMsg = new SignalingMessage(clientKey, nodeHost, (ConnectionEvent)tagged.type, tagged.body);
            StreamManagerHost.Instance?.HandleIncomingSignaling(clientKey, sigMsg);
        }
    }

    /// <summary>
    /// Envia informacion al servidor de node
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="clientId"></param>
    public async void SendToNode(SignalingMessage msg, string clientId)
    {
        if (ws?.State != WebSocketState.Open) return;

        if (!int.TryParse(clientId, out int idInt))
        {
            UnityEngine.Debug.LogError($"[WebSocketServerRTC] clientId invalido: {clientId}");
            return;
        }

        WSTaggedMessage tagged = new WSTaggedMessage { type = (int)msg.type, clientId = idInt, body = msg.body };
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(tagged));
        await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    /// <summary>
    /// Para el envio del mensaje de desconexion
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public async Task SendToNodeAsync(SignalingMessage msg, string clientId)
    {
        if (ws?.State != WebSocketState.Open) return;

        if (!int.TryParse(clientId, out int idInt))
        {
            UnityEngine.Debug.LogError($"[WebSocketServerRTC] clientId invalido: {clientId}");
            return;
        }

        WSTaggedMessage tagged = new WSTaggedMessage { type = (int)msg.type, clientId = idInt, body = msg.body };
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(tagged));
        await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    #endregion

    #region Monobehaviour
    public void Start()
    {
        string batPath = System.IO.Path.Combine(Application.dataPath, "..", batRelativePath);
        ws = new ClientWebSocket();
        nodeUri = new Uri($"ws://{nodeHost}:{nodePort}?type=unity");
        _cts = new CancellationTokenSource();

        StartCoroutine(WebRTC.Update());
        LaunchServer();
        ConnectToNode();
    }

    void OnDestroy()
    {
        DisconnectToNode();
        StopServer();
    }
    #endregion
}
