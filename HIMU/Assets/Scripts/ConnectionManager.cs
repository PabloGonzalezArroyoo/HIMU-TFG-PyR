using Assets.Scripts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.VisualScripting;
using UnityEditor.MemoryProfiler;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.Rendering.GPUSort;

public class ConnectionManager : MonoBehaviour
{
    // Singleton
    public static ConnectionManager Instance
    {
        get
        {
            return instance;
        }
    }
    private static ConnectionManager instance = null;

    // Connection UI
    [SerializeField]
    protected GameObject connectionUI;
    [SerializeField]
    protected GameObject successUI;
    [SerializeField]
    protected GameObject errorUI;
    [SerializeField]
    protected Image fadeImage;
    [SerializeField]
    protected float timeToFade = 2.5f;
    protected float timer = 0f;
    protected bool isFading = false;

    // Info general
    public ConnectionType connectionType = ConnectionType.USB;
    public bool isGamePad = true;

    // Lista de devices conectados
    protected List<string> devices = new List<string>();
    protected string deviceConnected = "";
    private bool running = false;
    bool mobileConnected = false;

    UdpClient broadcast;  //Para anunciar la IP del PC y permitir conexion luego
    [SerializeField] // Puerto por el cual el PC manda su info
    protected int broadcastPort = 8052;

    private UdpClient listener; // Para recibir info de cliente
    [SerializeField] // Puerto por el cual el PC recibe info
    protected int listenPort = 8053;
    private Thread listenThread;

    // Cosas de adb
    private string adbPath = "";

    private readonly Queue<InputInfo> inputEvents = new Queue<InputInfo>();
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
                deviceConnected = parts[0].Trim();
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
        listenThread = new Thread(ListenLoop) { IsBackground = true };
        listenThread.Start();
        UnityEngine.Debug.Log("[Host-PC] Escuchando UDP en el puerto : " + listenPort.ToString());
        InvokeRepeating(nameof(BroadcastIP), 0f, 2f);
    }

    private void ADBDisconnection()
    {
        devices.Clear();
        listener?.Close();
        listenThread?.Abort();
        RunAdbCommand("reverse --remove-all");
    }
    #endregion

    #region UDP

    string GetLocalIP()
    {
        foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        return "127.0.0.1";
    }

    void BroadcastIP()
    {
        if (mobileConnected) return; // no envía si ya hay conexión

        try
        {
            string localIP = GetLocalIP();
            DeviceInfo localInfo = new DeviceInfo(localIP, localIP);
            string json = JsonUtility.ToJson(new ConnectionInfo(ConnectionEvent.CONNECTION, localInfo));
            byte[] data = Encoding.UTF8.GetBytes(json);

            using (var sender = new UdpClient())
            {
                sender.EnableBroadcast = true;
                var endpoint = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
                sender.Send(data, data.Length, endpoint);
            }

            UnityEngine.Debug.Log($"[Host] Broadcast enviado → {json}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Host] Error al enviar broadcast: {e.Message}");
        }
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

                if (string.IsNullOrEmpty(message))
                {
                    UnityEngine.Debug.LogWarning("[UDP] Paquete vacío recibido, ignorando.");
                    continue;
                }

                // Mensaje de conexion inicial
                if (!mobileConnected)
                {
                    ConnectionInfo decodedData = JsonUtility.FromJson<ConnectionInfo>(message);

                    if (decodedData.infoDevice.deviceIP == "" || decodedData.connectionEvent != ConnectionEvent.CONNECTION) continue; // Mensaje no valido

                    deviceConnected = decodedData.infoDevice.deviceIP;
                    mobileConnected = true;
                    isFading = true;
                    successUI.SetActive(true);

                    UnityEngine.Debug.Log($"[Host] Cliente conectado: {deviceConnected}");
                    // El hilo ya está activo; simplemente seguimos en el mismo loop
                    continue;
                }

                // Mensajes comunes - Input
                if (mobileConnected)
                {
                    ConnectionInfo decodedData = JsonUtility.FromJson<ConnectionInfo>(message);
                    if (decodedData.infoDevice.deviceIP == deviceConnected || decodedData.connectionEvent == ConnectionEvent.DISCONNECTION)
                    {
                        HandleDisconnection();
                        continue;
                    }

                    InputInfo inputData = JsonUtility.FromJson<InputInfo>(message);
                    if (inputData.inputEvent == InputType.DEFAULT || inputData.deviceIdentifier.IsUnityNull()) continue; // MENSAJE NO VALIDO
                    if (inputData.deviceIdentifier.deviceIP != deviceConnected) continue; // OTRO DISPOSITIVO
                    lock (queueLock)
                        inputEvents.Enqueue(inputData);
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

    private void UDPDisconnection()
    {
        CancelInvoke(nameof(BroadcastIP));
        broadcast?.Close();
        listener?.Close();
        listenThread?.Abort();
    }
    #endregion

    #region Metodos comunes

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
                var endpoint = new IPEndPoint(IPAddress.Parse(deviceConnected), listenPort); // puerto del cliente
                sender.Send(data, data.Length, endpoint);
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Host] Error al enviar mensaje al cliente: {e.Message}");
        }
    }
    #endregion

    #region Disconnection

    public void HandleDisconnection()
    {
        running = false;
        mobileConnected = false;
        if (connectionType == ConnectionType.USB)
        {
            ADBDisconnection();
        }
        else
        {
            UDPDisconnection();
        }
        isFading = true;
        connectionUI?.SetActive(true);
        errorUI?.SetActive(true);
    }

    void OnDestroy()
    {
        HandleDisconnection();
    }
    #endregion

    private void Awake()
    {
        if (instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        instance = this;
        UnityEngine.Debug.Log(GetLocalIP());
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        running = true;
        if (connectionType == ConnectionType.USB)
        {
            adbPath = FindAdbPath();
            string output = RunAdbCommand("devices");
            StoreDeviceIds(output);
            while (deviceConnected == "")
            {
                output = RunAdbCommand("devices");
                StoreDeviceIds(output);
            }
            output = RunAdbCommand("reverse tcp:" + broadcastPort.ToString() + " tcp:" + broadcastPort.ToString());
        }

        StartListening();
    }

    // Update is called once per frame
    void Update()
    {
        if (isFading)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Clamp01(timer / timeToFade);
            Color c = fadeImage.color;
            c.a = alpha;
            fadeImage.color = c;

            if (timer >= timeToFade)
            {
                timer = 0f;
                isFading = false;
                connectionUI.SetActive(!mobileConnected);
                c.a = 0;
                fadeImage.color = c;
                successUI.SetActive(false);
                errorUI.SetActive(false);
                if (!running) SceneManager.LoadScene("MainMenu");
            }
        }

        if (running)
        {
            // Procesar mensajes en el hilo principal para poder usar la API de Unity
            lock (queueLock)
            {
                while (inputEvents.Count > 0)
                {
                    InputInfo e = inputEvents.Dequeue();
                    InputManager.Instance.OnInputReceived(deviceConnected, e);
                }
            }
        }
    }
}
