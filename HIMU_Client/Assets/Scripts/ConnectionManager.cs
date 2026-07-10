using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
    #region Variables

    /// <summary>
    /// Listener used for broadcast search of devices.
    /// </summary>
    private UdpClient listener;

    /// <summary>
    /// Thread where the broadcast will be done.
    /// </summary>
    private Thread listenThread;

    /// <summary>
    /// Wether the server is running or not.
    /// </summary>
    private bool running;

    /// <summary>
    /// Wether this machine is connected to a host or not.
    /// </summary>
    private bool connected;

    /// <summary>
    /// Port where this device will listen to upcoming network data.
    /// </summary>
    private int listenPort = 8053;

    /// <summary>
    /// Port where the host is located.
    /// </summary>
    private int hostPort;

    /// <summary>
    /// Host's IP.
    /// </summary>
    private string hostIP;

    /// <summary>
    /// This machine's IP.
    /// </summary>
    private string ipAddress;

    /// <summary>
    /// Multicast IP group for specific broadcasting.
    /// </summary>
    private const string MulticastGroup = "239.0.0.1";

    /// <summary>
    /// What type of client this device is (STREAM or PLAYER, NONE = non existent device).
    /// </summary>
    [SerializeField]
    private ClientType clientType;

    #endregion

    #region Connection

    /// <summary>
    /// Starts the broadcast loop to search for hosts to connect, via an UDP Client.
    /// </summary>
    /// <param name="c">Type of this client (setted ingame by the player's choice).</param>
    public void StartBroadcast(ClientType c)
    {
        if (!connected)
        { 
            clientType = c;
            Debug.Log("[ConnManager] Launching listen loop.");
            running = true;

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

    /// <summary>
    /// Uses and UDP listener to found the broadcast messages from the host machine. Once one is found,
    /// decodes the received data and starts the connection.
    /// </summary>
    private void BroadcastListenLoop()
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
            Debug.Log($"[ConnManager] Launched loop - {running} / {connected}"));
        while (running && !connected)
        {
            try
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log("[ConnManager] Listening."));
                var remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = listener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);
                UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log("[ConnManager] Receiving."));

                if (string.IsNullOrEmpty(message))
                {
                    Debug.LogWarning("[ConnManager] Empty UDP package, ignoring.");
                    continue;
                }

                ConnectionData decodedData = JsonUtility.FromJson<ConnectionData>(message);
                UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log(message));

                if (decodedData.connType != ConnectionEvent.BROADCAST)
                    continue;

                UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log("[ConnManager] Initialized connection."));
                OnConnectionStarted(decodedData);
            }
            catch (SocketException)
            {
                break; // Socket closed
            }
            catch (Exception e)
            {
                if (running)
                {
                    Debug.LogWarning($"[ConnManager] Broadcast thread error: {e.Message}");
                    //HandleDisconnection();
                }
            }
        }
    }

    /// <summary>
    /// Called once a valid broadcast message has been received. Reads and stores the message's information
    /// and creates a TCP connection, sending the handshake back to the host and delegates the work to the
    /// ClienSignalingHandler class.
    /// </summary>
    /// <param name="data"></param>
    public void OnConnectionStarted(ConnectionData data)
    {
        hostIP = data.ipAddress;
        hostPort = data.port;
        connected = true;

        UnityMainThreadDispatcher.Instance().Enqueue(() => Debug.Log($"[ConnManager] Host found: {hostIP} — answering…"));

        try
        {
            string json = JsonUtility.ToJson(new ConnectionData(ipAddress, listenPort, ConnectionEvent.HANDSHAKE, clientType));
            byte[] responseData = Encoding.UTF8.GetBytes(json);
            byte[] header = BitConverter.GetBytes(responseData.Length);

            TcpClient tcp = new TcpClient();
            tcp.Connect(hostIP, hostPort);
            NetworkStream stream = tcp.GetStream();
            NetworkUtils.WriteFramedMessage(stream, json);

            // Keep stream alive for signaling and communication in the connection handler class
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                Debug.Log($"[ConnManager] Handshake sent to {hostIP}:{hostPort}");
                ClientSignalingHandler.Instance?.StartSession(tcp, stream, hostIP);
                UIManager.Instance.OnConnectionStarted(hostIP);
            });
        }
        catch (Exception e)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                Debug.LogError($"[ConnManager] TCP connection error: {e.Message}"));
        }
    }

    #endregion

    #region Monobehaviour

    private void Start()
    {
        running = false;
        connected = false;
        ipAddress = NetworkUtils.GetIP();
        clientType = ClientType.NONE;
    }

    #endregion
}
