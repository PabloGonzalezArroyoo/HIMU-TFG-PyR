using Assets.Scripts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

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
    protected Image fadeImage;
    [SerializeField]
    protected float timeToFade = 2.5f;
    protected float timer = 0f;
    protected bool isFading = false;

    // Info general
    [SerializeField]
    protected ConnectionType connectionType = ConnectionType.USB;
    [SerializeField]
    protected bool isGamePad = true;

    protected string deviceIdentifier;
    private bool connected = false;

    // Config UDP
    private IPEndPoint remoteEndPoint;
    private UdpClient listener;
    private Thread listenThread;
    private bool running = false;
    private float sendTimer = 0f;

    private readonly System.Collections.Generic.Queue<string> messageQueue
        = new System.Collections.Generic.Queue<string>();
    private readonly object queueLock = new object();

    public int hostPort = 8052;
    public int listenPort = 8053;
    public float sendInterval = 3f;
    public string periodicMessage = "PING desde Android";

    public void ConnectUDP(string ip, int port)
    {
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        listener = new UdpClient();
        listener.Connect(remoteEndPoint);

        byte[] buffer = Encoding.UTF8.GetBytes("Conexion establecida");
        listener.Send(buffer, buffer.Length);

        connected = true;
        Debug.Log($"[UDP] Conectado a {ip}:{port}");
    }

    IEnumerator DiscoverAndConnect()
    {
        UdpClient listener = new UdpClient(9999);
        listener.EnableBroadcast = true;
        Debug.Log("[Mobile] Buscando PC en la red...");

        // Espera recibir el broadcast del PC
        var task = listener.ReceiveAsync();
        while (!task.IsCompleted) yield return null;

        string message = Encoding.UTF8.GetString(task.Result.Buffer);
        // Formato: "UNITY_CONTROLLER:192.168.1.50:8052"
        if (message.StartsWith("UNITY_CONTROLLER:"))
        {
            string[] parts = message.Split(':');
            string ip = parts[1];
            int port = int.Parse(parts[2]);
            listener.Close();
            Debug.Log("[Mobile] PC encontrado en " + ip + ":" + port);
            ConnectUDP(ip, port);
        }
    }

    #region ADB

    private void StartListening()
    {
        try
        {
            listener = new UdpClient(listenPort);
            listenThread = new Thread(ListenLoop) { IsBackground = true };
            listenThread.Start();
            Debug.Log($"[Client-Android] Escuchando mensajes del host en el puerto {listenPort}…");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Client-Android] No se pudo abrir el puerto {listenPort}: {e.Message}");
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

                lock (queueLock)
                    messageQueue.Enqueue(message);
            }
            catch (SocketException) { break; }
            catch (Exception e)
            {
                if (running)
                    Debug.LogWarning($"[Client-Android] Error en hilo de escucha: {e.Message}");
            }
        }
    }

    private bool SendUdpToHost(string message)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            var endpoint = new IPEndPoint(IPAddress.Loopback, hostPort); // 127.0.0.1

            using (var sender = new UdpClient())
                sender.Send(data, data.Length, endpoint);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Client-Android] Error al enviar UDP: {e.Message}");
            return false;
        }
    }

    public void SendToHost(string message)
    {
        if (!connected)
        {
            Debug.LogWarning("[Client-Android] No conectado al host.");
            return;
        }

        SendUdpToHost(message);
        Debug.Log($"[Client-Android] Mensaje enviado al host: {message}");
    }
    #endregion

    private void SendHello()
    {
        // Incluye info del dispositivo para que el host pueda identificarlo
        string message = "CLIENT_HELLO:" + deviceIdentifier;

        if (SendUdpToHost(message))
        {
            connected = true;
            Debug.Log($"[Client-Android] Handshake enviado al host (127.0.0.1:{hostPort})");
        }
        else
        {
            Debug.LogError("[Client-Android] No se pudo enviar el handshake. " +
                           "¿Está activo el adb reverse en el PC?");
        }
    }

    #region Getters/Setters
    private string CreateDeviceIdentifier()
    {
        if (connectionType == ConnectionType.USB)
        {
            return SystemInfo.deviceUniqueIdentifier;
        }
        string uid = SystemInfo.deviceUniqueIdentifier;
        string ipaddress = "";
        try
        {
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    ipaddress = ip.ToString();
        }
        catch (System.Exception e) { Debug.LogError(e); }
        ipaddress = "No disponible";
        return ipaddress;
    }

    public string GetDeviceInfo()
    {
        return deviceIdentifier;
    }

    #endregion

    public void EnviarDatos(InputInfo datos)
    {
        EnviarDatosAsync(datos);
    }

    private async Task EnviarDatosAsync(InputInfo datos)
    {
        if (listener == null || !connected)
        {
            Debug.LogWarning("[UDP] No hay conexion activa.");
            return;
        }

        // Serializar el struct a JSON y luego a bytes
        // UDP es orientado a datagramas: no se necesita enviar la longitud por separado,
        // cada Send() es un datagrama completo e independiente.
        string json = JsonUtility.ToJson(datos);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        await listener.SendAsync(buffer, buffer.Length);
    }

    private void Awake()
    {
        if (instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);

        deviceIdentifier = CreateDeviceIdentifier();

        Debug.Log(deviceIdentifier);


        if (connectionType == ConnectionType.USB)
        {
            ConnectUDP("127.0.0.1", hostPort);
        }
        else
        {
            StartCoroutine(DiscoverAndConnect());
        }
    }

    void OnDestroy()
    {
        listener?.Close();
        listenThread?.Abort();
        listener = null;
        Debug.Log("[UDP] Conexion cerrada.");
    }

    private void Update()
    {
        if (connected && isFading)
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
                c.a = 1;
                fadeImage.color = c;
                connectionUI.SetActive(false);
            }
        }
    }

    //private void Start()
    //{
    //    running = true;

    //    // Arranca el listener antes del handshake para no perder respuestas inmediatas
    //    StartListening();

    //    // Inicia el handshake
    //    SendHello();
    //}

    //private void Update()
    //{
    //    // Procesar mensajes del host en el hilo principal
    //    lock (queueLock)
    //    {
    //        while (messageQueue.Count > 0)
    //            OnMessageReceived(messageQueue.Dequeue());
    //    }

    //    // Envío periódico
    //    if (!connected) return;

    //    sendTimer += Time.deltaTime;
    //    if (sendTimer >= sendInterval)
    //    {
    //        sendTimer = 0f;
    //        SendToHost(periodicMessage);
    //    }
    //}
}
