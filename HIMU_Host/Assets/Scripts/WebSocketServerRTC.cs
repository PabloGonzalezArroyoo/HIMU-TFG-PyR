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
using UnityEditor.PackageManager;
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
    /// Metodo que desbloquea el bat para poder ejecutarlo
    /// </summary>
    /// <param name="path"></param>
    void UnblockFile(string path)
    {
        string zoneIdentifierPath = path + ":Zone.Identifier";
        try
        {
            if (System.IO.File.Exists(zoneIdentifierPath))
            {
                System.IO.File.Delete(zoneIdentifierPath);
                UnityEngine.Debug.Log("[ServerLauncher] Archivo desbloqueado correctamente.");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[ServerLauncher] No se pudo desbloquear el archivo: {ex.Message}");
        }
    }

    /// <summary>
    /// PID del proceso del bat lanzado, para poder cerrarlo directamente
    /// sin depender de buscar por titulo de ventana (poco fiable).
    /// </summary>
    Process launchedBatProcess;

    /// <summary>
    /// Lanza el servidor de Node ejecutando el archivo .bat
    /// </summary>
    public void LaunchServer()
    {
        UnblockFile(batPath);
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = batPath,
            WorkingDirectory = System.IO.Path.GetDirectoryName(batPath),
            UseShellExecute = true,
            CreateNoWindow = false
        };

        try
        {
            launchedBatProcess = Process.Start(psi);
            UnityEngine.Debug.Log($"[ServerLauncher] Script lanzado correctamente. PID: {launchedBatProcess?.Id}");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[ServerLauncher] Error al lanzar el bat: {ex.Message}");
        }
    }

    /// <summary>
    /// Detiene el servidor de Node, el servidor HTML y la ventana del bat
    /// </summary>
    public void StopServer()
    {
        KillProcessOnPort(nodePort);  // 8080 - server.js
        KillProcessOnPort(3000);      // 3000 - npx serve

        if (launchedBatProcess != null)
        {
            try
            {
                ProcessStartInfo killPsi = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {launchedBatProcess.Id} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(killPsi)?.WaitForExit();
                UnityEngine.Debug.Log($"[ServerLauncher] Ventana del bat (PID {launchedBatProcess.Id}) cerrada via taskkill /T.");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[ServerLauncher] No se pudo cerrar el PID guardado: {ex.Message}");
            }
        }

        // Fallback: tambien intentamos por titulo por si alguna razon el PID guardado no es el que contiene la ventana visible, .
        foreach (var process in Process.GetProcessesByName("cmd"))
        {
            try
            {
                UnityEngine.Debug.Log($"[ServerLauncher] cmd PID {process.Id} titulo: '{process.MainWindowTitle}'");
                if (process.MainWindowTitle == "Servidor de Streaming")
                {
                    process.Kill();
                    UnityEngine.Debug.Log("[ServerLauncher] Ventana del bat cerrada (fallback por titulo).");
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Busca que proceso esta escuchando en un puerto TCP dado usando netstat, y lo mata por PID con taskkill
    /// </summary>
    void KillProcessOnPort(int port)
    {
        try
        {
            ProcessStartInfo netstatPsi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c netstat -ano | findstr :{port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (Process netstat = Process.Start(netstatPsi))
            {
                string output = netstat.StandardOutput.ReadToEnd();
                netstat.WaitForExit();

                var seenPids = new HashSet<string>();
                foreach (string line in output.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // Ultima columna de netstat -ano es el PID
                    string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 1) continue;

                    string pid = parts[parts.Length - 1];
                    if (!int.TryParse(pid, out _)) continue;
                    if (!seenPids.Add(pid)) continue; // evitar matar el mismo PID varias veces

                    ProcessStartInfo killPsi = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {pid} /F /T",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(killPsi)?.WaitForExit();
                    UnityEngine.Debug.Log($"[ServerLauncher] Proceso en puerto {port} (PID {pid}) cerrado.");
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[ServerLauncher] No se pudo cerrar el puerto {port}: {ex.Message}");
        }
    }

    /// <summary>
    /// Inicia la conexion al servidor de Node (con reintentos por si se llega a ejecutar antes que el LaunchServer acabe)
    /// </summary>
    public async void ConnectToNode()
    {
        int maxRetries = 10;
        int delayMs = 1500;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                ws = new ClientWebSocket(); // recrear, un ClientWebSocket fallido no se puede reusar
                await ws.ConnectAsync(nodeUri, CancellationToken.None);
                UnityEngine.Debug.Log($"[StreamManager] Conectado a Node: {nodeUri}");
                _ = ReceiveLoop(_cts);
                return; // éxito, salir
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[StreamManager] Intento {attempt}/{maxRetries} fallido: {ex.Message}");
                await Task.Delay(delayMs);
            }
        }

        UnityEngine.Debug.LogError("[StreamManager] No se pudo conectar a Node tras varios intentos.");
    }

    /// <summary>
    /// Detiene la conexion al servidor de Node
    /// </summary>
    public async Task DisconnectToNode()
    {
        if (ws?.State == WebSocketState.Open && connectedBrowsers.Count > 0)
        {
            SignalingMessage byeMsg = new SignalingMessage(nodeHost, null, ConnectionEvent.DISCONNECT, null);

            // copia las claves para no modificar la colección mientras iteras
            foreach (string clientId in new List<string>(connectedBrowsers.Keys))
            {
                await SendToNodeAsync(byeMsg, clientId);
                StreamManager.Instance.RemovePeerForBrowser(clientId);
            }

        }

        _cts?.Cancel();
        if (ws?.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);

        connectedBrowsers.Clear();
        UIManager.Instance?.ResetStreamClientsText();
        _cts.Dispose();
        ws.Dispose();
        _cts = new CancellationTokenSource();
        ws = new ClientWebSocket();

        UnityEngine.Debug.Log("[WebSocketServerRTC] Clientes del servidor NODE borrados");
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
        }
        catch (OperationCanceledException)
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
            ClientData client = new ClientData(connData, null, clientKey);

            connectedBrowsers[clientKey] = client;
            UnityEngine.Debug.Log($"[StreamManager] Browser registrado: {clientKey} (total: {connectedBrowsers.Count})");

            StreamManager.Instance?.CreatePeerForBrowser(client);
            UIManager.Instance?.UpdateStreamClientsText(true);

        }
        else if (baseMsg.type == (int)ConnectionEvent.DISCONNECT) // un navegador se desconectó del lado de Node
        {
            WSTaggedMessage tagged = JsonUtility.FromJson<WSTaggedMessage>(rawJson);
            string clientKey = tagged.clientId.ToString();

            if (connectedBrowsers.Remove(clientKey))
                UnityEngine.Debug.Log($"[StreamManager] Browser eliminado del registro: {clientKey}");

            StreamManager.Instance?.RemovePeerForBrowser(clientKey);
            UIManager.Instance?.UpdateStreamClientsText(false);
        }
        else // SDP o ICE de un browser existente
        {
            WSTaggedMessage tagged = JsonUtility.FromJson<WSTaggedMessage>(rawJson);
            string clientKey = tagged.clientId.ToString();
            SignalingMessage sigMsg = new SignalingMessage(clientKey, nodeHost, (ConnectionEvent)tagged.type, tagged.body);
            StreamManager.Instance?.HandleIncomingSignaling(clientKey, sigMsg);
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

    public void ChangeVideoTrack(VideoStreamTrack newTrack)
    {
        foreach (var client in connectedBrowsers.Values) // o la colección donde tengas tus WebRTCPeer
        {
            RTCRtpSender sender = client.webRtcPeer.GetVideoSender(); // necesitas exponer esto en tu clase WebRTCPeer si no lo tienes
            sender?.ReplaceTrack(newTrack);
        }
    }
    #endregion

    #region Monobehaviour
    public void Start()
    {
        nodeHost = StreamManager.Instance.GetIP();
        batPath = System.IO.Path.Combine(Application.dataPath, "..", batRelativePath);
        ws = new ClientWebSocket();
        nodeUri = new Uri($"ws://{nodeHost}:{nodePort}?type=unity");
        _cts = new CancellationTokenSource();

        StartCoroutine(WebRTC.Update());
        LaunchServer();
        ConnectToNode();
    }

    void OnDestroy()
    {
        try
        {
            DisconnectToNode();
            StopServer();
        } catch { }
    }
    #endregion
}