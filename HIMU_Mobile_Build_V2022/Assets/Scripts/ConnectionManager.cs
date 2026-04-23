using Assets.Scripts;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
    public string hostIP = "";
    private bool connected = false;

    // Config UDP
    private IPEndPoint remoteEndPoint;
    private UdpClient listener;
    private Thread listenThread;
    private bool running = false;

    private readonly System.Collections.Generic.Queue<string> messageQueue
        = new System.Collections.Generic.Queue<string>();
    private readonly object queueLock = new object();

    public int hostPort = 8053;
    public int listenPort = 8052;

    private void StartBroadcastListen()
    {
        listener = new UdpClient(listenPort);

        listenThread = new Thread(BroadcastListenLoop) { IsBackground = true };
        listenThread.Start();
    }

    private void BroadcastListenLoop()
    {
        while (running && !connected)
        {
            try
            {
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);

                if (message.StartsWith("HOST_ANNOUNCE:"))
                {
                    string ip = message.Replace("HOST_ANNOUNCE:", "").Trim();

                    // Evitar conectarse a sí mismo si host y cliente corren en la misma máquina
                    if (ip == deviceIdentifier) continue;

                    hostIP = ip;
                    connected = true;

                    Debug.Log($"[Client] Host encontrado: {hostIP} — enviando respuesta…");
                    string response = "CLIENT_HELLO:" + deviceIdentifier;
                    SendToHost(response, IPAddress.Parse(hostIP));
                }
            }
            catch (SocketException)
            {
                break; // Socket cerrado
            }
            catch (Exception e)
            {
                if (running)
                    Debug.LogWarning($"[Client] Error en hilo de broadcast: {e.Message}");
            }
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
    private void SendHello()
    {
        // Incluye info del dispositivo para que el host pueda identificarlo
        string message = "CLIENT_HELLO:" + deviceIdentifier;

        if (SendToHost(message, IPAddress.Loopback))
        {
            connected = true;
            Debug.Log("[Client-Android] Handshake enviado al host 127.0.0.1:" + listenPort.ToString());
        }
        else
        {
            Debug.LogError("[Client-Android] No se pudo enviar el handshake. " +
                           "¿Está activo el adb reverse en el PC?");
            connected = false;
        }
    }
    #endregion

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


    public bool SendToHost(string message, IPAddress address)
    {
        if (!connected || string.IsNullOrEmpty(hostIP))
        {
            Debug.LogWarning("[Client-Android] No conectado al host.");
            return false;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            var endpoint = new IPEndPoint(address, hostPort); // 127.0.0.1

            using (var sender = new UdpClient())
                sender.Send(data, data.Length, endpoint);

            Debug.Log($"[Client-Android] Mensaje enviado al host: {message}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Client-Android] Error al enviar UDP: {e.Message}");
            connected = false;
            hostIP = "";
            return false;
        }
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
    }

    private void Start()
    {
        running = true;
        deviceIdentifier = CreateDeviceIdentifier();
        Debug.Log(deviceIdentifier);

        if (connectionType == ConnectionType.USB)
        {
            StartListening();
            SendHello();
        }
        else
        {
            StartBroadcastListen();
        }
    }

    void OnDestroy()
    {
        running = false;
        connected = false;
        hostIP = null;
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

        // Que hacer si running == false
    }

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
