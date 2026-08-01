using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.WebRTC;
using UnityEngine;

public class WebSocketServerRTC : MonoBehaviour
{
    // TO-DO -> COMENTARIOS EN INGLÉS
    #region Variables

    /// <summary>
    /// Debe ser la IP del dispositivo que corre el servidor de Node (asumimos que es esta maquina misma)
    /// </summary>
    public string nodeHost = "192.168.1.45";
    [SerializeField]
    private int nodePort = 8080;
    [SerializeField]
    private int browserPort = 3000;
    private int sessionID = 1234;
    private bool running = false;
    /// <summary>
    /// Socket de conexion al servidor de Node
    /// </summary>
    private ClientWebSocket ws;

    /// <summary>
    /// PID del proceso del bat lanzado, para poder cerrarlo directamente
    /// sin depender de buscar por titulo de ventana (poco fiable).
    /// </summary>
    private Process launchedBatProcess;

    /// <summary>
    /// Ruta del bat desde la carpeta raiz de la build o desde la carpeta raiz del proyecto
    /// </summary>
    [SerializeField]
    private string batRelativePath = "start-server.bat";
    private string batPath;

    /// <summary>
    /// Se usa para cancelar la task asincrona del ReceiveLoop
    /// </summary>
    private CancellationTokenSource _cts;

    /// <summary>
    /// Direccion del servidor de Node
    /// </summary>
    private Uri nodeUri;

    #endregion

    #region Bat

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
    #endregion

    #region Connection

    /// <summary>
    /// Lanza el servidor de Node ejecutando el archivo .bat
    /// </summary>
    public void LaunchServer()
    {
        running = true;
        UnblockFile(batPath);
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = batPath,
            Arguments = $"{nodePort} {browserPort}",
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
        running = false;
        KillProcessOnPort(nodePort);
        KillProcessOnPort(browserPort);

        string windowTitle = "Streaming server";

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /T /FI \"WINDOWTITLE eq {windowTitle}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();
                UnityEngine.Debug.Log($"[ServerLauncher] taskkill por titulo '{windowTitle}': {output.Trim()}{error.Trim()}");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[ServerLauncher] No se pudo cerrar la ventana '{windowTitle}': {ex.Message}");
        }

        launchedBatProcess = null;
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
                UnityEngine.Debug.Log($"[WebSocketServerRTC] Connected to Node: {nodeUri}");
                _ = ReceiveLoop(_cts);
                return; // éxito, salir
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[WebSocketServerRTC] Failed attemp {attempt}/{maxRetries}: {ex.Message}");
                await Task.Delay(delayMs);
            }
        }

        UnityEngine.Debug.LogError("[WebSocketServerRTC] Couldn't connect to Node after the maximum attemps.");
    }

    /// <summary>
    /// Detiene la conexion al servidor de Node
    /// </summary>
    public async Task DisconnectToNode()
    {
        var browserClients = StreamManager.Instance.GetClients()
            .Where(c => c.transport == ConnectionTransport.WebSocket).ToList();

        if (ws?.State == WebSocketState.Open && browserClients.Count > 0)
        {
            SignalingMessage byeMsg = new SignalingMessage(ConnectionEvent.DISCONNECT, null);

            // copia las claves para no modificar la colección mientras iteras
            foreach (var client in browserClients)
            {
                await SendToNode(byeMsg, client.identifier);
                StreamManager.Instance.RemovePeer(client.identifier);
            }
        }

        _cts?.Cancel();
        if (ws?.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);

        UIManager.Instance?.ResetStreamClientsText();
        _cts.Dispose();
        ws.Dispose();
        _cts = new CancellationTokenSource();
        ws = new ClientWebSocket();

        UnityEngine.Debug.Log("[WebSocketServerRTC] Node clients deleted.");
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
                    UnityEngine.Debug.LogError($"[WebSocketServerRTC] ReceiveLoop: {ex.Message}");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            UnityEngine.Debug.LogError($"[WebSocketServerRTC] ReceiveLoop: Se detuvo el proceso de recepcion de informacion del servidor de Node");
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
            ClientData client = ClientData.ForBrowser(clientKey, clientKey);

            StreamManager.Instance?.CreatePeerForBrowser(client);
            UnityEngine.Debug.Log($"[WebSocketServerRTC] Browser registered: {clientKey}");

            UIManager.Instance?.UpdateStreamClientsText(true);

        }
        else if (baseMsg.type == (int)ConnectionEvent.DISCONNECT) // un navegador se desconectó del lado de Node
        {
            WSTaggedMessage tagged = JsonUtility.FromJson<WSTaggedMessage>(rawJson);
            string clientKey = tagged.clientId.ToString();

            StreamManager.Instance?.RemovePeer(clientKey);
            UnityEngine.Debug.Log($"[WebSocketServerRTC] Browser deleted from register: {clientKey}");

            UIManager.Instance?.UpdateStreamClientsText(false);
        }
        else // SDP o ICE de un browser existente
        {
            WSTaggedMessage tagged = JsonUtility.FromJson<WSTaggedMessage>(rawJson);
            SignalingMessage sigMsg = new SignalingMessage((ConnectionEvent)tagged.type, tagged.body);
            string clientKey = tagged.clientId.ToString();
            StreamManager.Instance?.HandleIncomingSignaling(clientKey, sigMsg);
        }
    }

    /// <summary>
    /// Sends date to node's server.
    /// </summary>
    /// <param name="msg">Signaling message.</param>
    /// <param name="clientId">Which client seends the date.</param>
    /// <returns></returns>
    public async Task SendToNode(SignalingMessage msg, string clientId)
    {
        if (ws?.State != WebSocketState.Open) return;

        if (!int.TryParse(clientId, out int idInt))
        {
            UnityEngine.Debug.LogError($"[WebSocketServerRTC] Invalid clientId: {clientId}");
            return;
        }

        try
        {
            WSTaggedMessage tagged = new WSTaggedMessage { type = (int)msg.type, clientId = idInt, body = msg.body };
            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(tagged));
            await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[WebSocketServerRTC] Error sending message to Node (clientId={clientId}): {ex.Message}");
        }
    }

    public void ChangeVideoTrack(VideoStreamTrack newTrack)
    {
        foreach (var client in StreamManager.Instance.GetClients()
            .Where(c => c.transport == ConnectionTransport.WebSocket))
        {
            RTCRtpSender sender = client.webRtcPeer?.GetVideoSender();
            sender?.ReplaceTrack(newTrack);
        }
    }

    #endregion

    #region Getters&Setters
    public string GetNodeHost()
    {
        return nodeHost;
    }

    public int GetBrowserPort()
    {
        return browserPort;
    }
    #endregion

    #region Monobehaviour
    public void Start()
    {
        System.Random rnd = new System.Random();
        sessionID = rnd.Next(1000, 10000);
        nodeHost = NetworkUtils.GetIP();
        batPath = System.IO.Path.Combine(Application.dataPath, "..", batRelativePath);
        ws = new ClientWebSocket();
        nodeUri = new Uri($"ws://{nodeHost}:{nodePort}?type=unity&id={sessionID}");
        _cts = new CancellationTokenSource();
    }

    void OnDestroy()
    {
        if (!running) return;
        try
        {
            _ = DisconnectToNode();
            StopServer();
        }
        catch { }
    }
    #endregion
}