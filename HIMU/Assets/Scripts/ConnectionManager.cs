using Assets.Scripts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
    // Info general
    public ConnectionType connectionType = ConnectionType.USB;
    public bool isGamePad = true;

    // Cosas de adb
    private string adbPath = "";

    // Lista de devices conectados
    protected List<string> devices = new List<string>();
    protected string deviceConnected = "";

    UdpClient broadcast;  //Para anunciar la IP del PC y permitir conexion luego
    [SerializeField]
    protected int broadcastPort = 8052;
    [SerializeField]
    protected int listenPort = 8053;

    private UdpClient listener;
    private Thread listenThread;
    private bool running = false;
    bool mobileConnected = false;
    private readonly Queue<string> inputEvents = new Queue<string>();
    private readonly object queueLock = new object();

    #region ADB
    private string FindAdbPath()
    {
        string username = System.Environment.UserName;
        string localAppData = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.LocalApplicationData);

        string[] candidatas = {
        // Variable de entorno estándar
        System.IO.Path.Combine(
            System.Environment.GetEnvironmentVariable("ANDROID_HOME") ?? "",
            "platform-tools", "adb.exe"),
        System.IO.Path.Combine(
            System.Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? "",
            "platform-tools", "adb.exe"),

        // Ruta por defecto de Android Studio
        System.IO.Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe"),

        // Ruta si instalaste el SDK manualmente
        @"C:\Android\sdk\platform-tools\adb.exe",
        @"C:\android-sdk\platform-tools\adb.exe",
    };

        foreach (string ruta in candidatas)
        {
            if (!string.IsNullOrEmpty(ruta) && System.IO.File.Exists(ruta))
            {
                UnityEngine.Debug.Log($"adb encontrado en: {ruta}");
                return ruta;
            }
        }

        UnityEngine.Debug.LogError("No se encontró adb.exe. Instala Android Studio o el Android SDK.");
        return null;
    }

    private void StoreDeviceIds(string adbOutput)
    {
        string[] lines = adbOutput.Split('\n');

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            // Saltamos la cabecera y líneas vacías
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("List of devices"))
                continue;

            // Cada dispositivo válido tiene formato: "<id>\t<estado>"
            string[] parts = trimmed.Split('\t');
            if (parts.Length >= 2 && parts[1].Trim() == "device")
            {
                devices.Add(parts[0].Trim());
                UnityEngine.Debug.Log(parts[0]);
            }
        }
    }

    public string RunAdbCommand(string arguments)
    {
        var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (error.Length > 0)
            UnityEngine.Debug.LogWarning("ADB Error: " + error);

        return output.ToString();
    }

    private void StartListening()
    {
        listener = new UdpClient(listenPort);
        listenThread = new Thread(ListenLoopADB) { IsBackground = true };
        listenThread.Start();
        UnityEngine.Debug.Log($"[Host-PC] Escuchando UDP en el puerto {listenPort}…");
    }

    private void ListenLoopADB()
    {
        while (running)
        {
            try
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);

                // ── Handshake ─────────────────────────────────────────────────
                if (!mobileConnected && message.StartsWith("CLIENT_HELLO:"))
                {
                    deviceConnected = message.Replace("CLIENT_HELLO:", "").Trim();
                    mobileConnected = true;
                    UnityEngine.Debug.Log($"[Host-PC] Cliente Android conectado. Info: {deviceConnected}");
                    continue;
                }

                // ── Mensajes normales ─────────────────────────────────────────
                if (mobileConnected)
                {
                    lock (queueLock)
                        inputEvents.Enqueue(message);
                }
            }
            catch (SocketException) { break; }
            catch (Exception e)
            {
                if (running)
                    UnityEngine.Debug.LogWarning($"[Host-PC] Error en hilo de escucha: {e.Message}");
            }
        }
    }

    public void ConfigureADB()
    {
        adbPath = FindAdbPath();
        string output = RunAdbCommand("devices");
        StoreDeviceIds(output);
        output = RunAdbCommand("reverse tcp:8052 tcp:8052");

    }
    #endregion

    #region UDP

    void BroadcastIP()
    {
        if (mobileConnected) return; // no envía si ya hay conexión

        try
        {
            string localIP = GetLocalIP();
            string message = "UNITY_CONTROLLER:" + localIP + broadcast.ToString();
            byte[] data = Encoding.UTF8.GetBytes(message);

            using (var sender = new UdpClient())
            {
                sender.EnableBroadcast = true;
                var endpoint = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
                sender.Send(data, data.Length, endpoint);
            }

            UnityEngine.Debug.Log($"[Host] Broadcast enviado → {message}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Host] Error al enviar broadcast: {e.Message}");
        }
    }

    private void StartListening()
    {
        listener = new UdpClient(listenPort);

        listenThread = new Thread(ListenLoop) { IsBackground = true };
        listenThread.Start();

        UnityEngine.Debug.Log($"[Host] Escuchando en el puerto {listenPort}…");
    }

    private void ListenLoop()
    {
        while (running)
        {
            try
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);

                // ── Handshake: el cliente responde con su IP ──────────────────
                if (!mobileConnected && message.StartsWith("CLIENT_HELLO:"))
                {
                    string ip = message.Replace("CLIENT_HELLO:", "").Trim();
                    deviceConnected = ip;
                    mobileConnected = true;

                    UnityEngine.Debug.Log($"[Host] Cliente conectado: {deviceConnected}");
                    // El hilo ya está activo; simplemente seguimos en el mismo loop
                    continue;
                }

                // ── Mensajes normales del cliente ─────────────────────────────
                if (mobileConnected)
                {
                    lock (queueLock)
                    {
                        inputEvents.Enqueue(message);
                    }
                }
            }
            catch (SocketException)
            {
                // El socket se cerró: salir limpiamente
                break;
            }
            catch (Exception e)
            {
                if (running)
                    UnityEngine.Debug.LogWarning($"[Host] Error en hilo de escucha: {e.Message}");
            }
        }
    }

    // ── Callback de mensajes ──────────────────────────────────────────────────

    /// <summary>
    /// Se llama en el hilo principal cada vez que llega un mensaje del cliente.
    /// Personaliza esta función para reaccionar a los datos recibidos.
    /// </summary>
    private void OnMessageReceived(string message)
    {
        UnityEngine.Debug.Log($"[Host] Mensaje de {deviceConnected}: {message}");
        // → Aquí puedes parsear el mensaje y actualizar el estado del juego
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>Envía un mensaje UDP al cliente conectado.</summary>
    public void SendToClient(string message)
    {
        if (!mobileConnected || string.IsNullOrEmpty(deviceConnected))
        {
            UnityEngine.Debug.LogWarning("[Host] No hay cliente conectado.");
            return;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            using (var sender = new UdpClient())
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(deviceConnected), listenPort - 1); // puerto del cliente
                sender.Send(data, data.Length, endpoint);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Host] Error al enviar mensaje al cliente: {e.Message}");
        }
    }

    public void ConfigureUDP()
    {
        running = true;

        // Arranca el socket de escucha antes de empezar a hacer broadcast
        StartListening();

        // Envía broadcasts hasta conectarse
        InvokeRepeating(nameof(BroadcastIP), 0f, 2f);
    }

    #endregion

    private void Awake()
    {
        if (connectionType == ConnectionType.USB)
        {
            ConfigureADB();
        }
        else
        {
            ConfigureUDP();
        }
    }

    void OnDestroy()
    {
        running = false;
        CancelInvoke(nameof(BroadcastIP));
        broadcast?.Close();
        listener?.Close();
        listenThread?.Abort();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Procesar mensajes en el hilo principal para poder usar la API de Unity
        lock (queueLock)
        {
            while (inputEvents.Count > 0)
            {
                string msg = inputEvents.Dequeue();
                OnMessageReceived(msg);
            }
        }
    }

    #region USB

    #endregion

    string GetLocalIP()
    {
        foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        return "127.0.0.1";
    }
}
