using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Assets.Scripts;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    private UdpClient listener;
    private Thread listenThread;

    private bool running = false;
    private bool connected = false;

    private int listenPort = 8053;
    private int hostPort;

    private string hostIP;
    public static string ipAddress { get; private set; }

    public void StartBroadcast()
    {
        if (!connected)
        {
            Debug.Log("Lanzando listen loop");
            running = true;
            GetIpAddress();

            listener = new UdpClient(listenPort);
            listenThread = new Thread(BroadcastListenLoop) { IsBackground = true };
            listenThread.Start();
        }
    }

    private void BroadcastListenLoop()
    {
        while (running && !connected)
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

                if (decodedData.type != ConnectionEvent.BROADCAST)
                    continue;

                OnConnectionStarted(decodedData);
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

    private void GetIpAddress()
    {
        ipAddress = "No disponible";
        try
        {
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    ipAddress = ip.ToString();
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
        }
    }

    public void OnConnectionStarted(ConnectionData data)
    {
        hostIP = data.ipAddress;
        hostPort = data.port;
        connected = true;

        Debug.Log($"[Client] Host encontrado: {hostIP} — enviando respuesta…");

        string json = JsonUtility.ToJson(new ConnectionData(ipAddress, listenPort, ConnectionEvent.HANDSHAKE));
        byte[] responseData = Encoding.UTF8.GetBytes(json);

        TcpClient tcp = new TcpClient();
        tcp.Connect(hostIP, hostPort);
        NetworkStream stream = tcp.GetStream();
        stream.Write(responseData, 0, responseData.Length);

        Debug.Log($"[Client] Handshake enviado a {hostIP}:{hostPort}");

        // Mantener el stream vivo para señalización
        UnityMainThreadDispatcher.Instance().Enqueue(() => 
            ClientSignalingHandler.Instance?.StartSession(stream, hostIP));

        UIManager.Instance.OnConnectionStarted(hostIP);
    }

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

    }
}
