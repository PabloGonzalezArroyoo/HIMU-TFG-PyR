using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    private UdpClient listener;
    private Thread listenThread;

    private bool running;
    private bool connected;

    private int listenPort = 8053;
    private int hostPort;

    private string hostIP;
    public static string ipAddress { get; private set; }

    private const string MulticastGroup = "239.0.0.1";

    [SerializeField]
    private ClientType clientType;

    public void StartBroadcast(ClientType c)
    {
        if (!connected)
        { 
            clientType = c;
            Debug.Log("[Cliente] Lanzando listen loop");
            running = true;
            GetIpAddress();

            listener = new UdpClient();
            listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Client.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            //listener.JoinMulticastGroup(IPAddress.Parse(MulticastGroup), IPAddress.Parse(ipAddress));
            listener.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse(MulticastGroup), IPAddress.Any));

            listenThread = new Thread(BroadcastListenLoop) { IsBackground = true };
            listenThread.Start();
        }
    }

    private void BroadcastListenLoop()
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
            Debug.Log($"[Client] Loop arrancado - {running} / {connected}"));
        while (running && !connected)
        {
            try
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log("[Client] Escuchando"));
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);
                UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log("[Client] Recibiendo"));

                if (string.IsNullOrEmpty(message))
                {
                    Debug.LogWarning("[UDP] Paquete vacío recibido, ignorando.");
                    continue;
                }

                ConnectionData decodedData = JsonUtility.FromJson<ConnectionData>(message);
                UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log(message));

                if (decodedData.connType != ConnectionEvent.BROADCAST)
                    continue;

                UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log("[Client] Conexión iniciada"));
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
                    Debug.Log($"[Network] Adaptador: {ni.Name} — IP: {ipAddress}");
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

    public void OnConnectionStarted(ConnectionData data)
    {
        hostIP = data.ipAddress;
        hostPort = data.port;
        connected = true;

        UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log($"[Client] Host encontrado: {hostIP} — enviando respuesta…"));

        try
        {
            string json = JsonUtility.ToJson(new ConnectionData(ipAddress, listenPort, ConnectionEvent.HANDSHAKE, clientType));
            byte[] responseData = Encoding.UTF8.GetBytes(json);
            TcpClient tcp = new TcpClient();
            tcp.Connect(hostIP, hostPort);
            NetworkStream stream = tcp.GetStream();
            stream.Write(responseData, 0, responseData.Length);
            // Mantener el stream vivo para señalización
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                Debug.Log($"[Client] Handshake enviado a {hostIP}:{hostPort}");
                ClientSignalingHandler.Instance?.StartSession(stream, hostIP);
                UIManager.Instance.OnConnectionStarted(hostIP);
            });
        }
        catch (Exception e)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                Debug.LogError($"[Client] Error en TCP connect: {e.Message}"));
        }
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
        running = false;
        connected = false;
        clientType = ClientType.NONE;
    }
}
