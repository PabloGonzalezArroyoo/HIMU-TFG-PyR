using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.WebRTC;
using UnityEditor.PackageManager;
using UnityEngine;

public class SignalingServer : MonoBehaviour
{

    #region Variables
    /// <summary>
    /// Wether if the server is running or not
    /// </summary>
    bool running;

    /// <summary>
    /// Wether if the server is searching for new devieces or not
    /// </summary>
    bool searchingDevices;

    /// <summary>
    /// Port where the server will listen to upcoming network data
    /// </summary>
    int listenPort = 7777;

    /// <summary>
    /// Port from where the broadcast is going to be made
    /// </summary>
    int broadcastPort = 8053;

    /// <summary>
    /// Direccion IP de la maquina host
    /// </summary>
    string ipAddress;

    /// <summary>
    /// Listener para escuchar mensajes de los clientes
    /// </summary>
    TcpListener listener;

    /// <summary>
    /// Hilo de escucha de mensajes
    /// </summary>
    Thread listenThread;

    /// <summary>
    /// Tamaño del buffer de mensajeria
    /// </summary>
    int bufferSize;
    
    /// <summary>
    /// Estructura de clientes 
    /// </summary>
    private readonly ConcurrentDictionary<string, TcpClient> clients = new();

    /// <summary>
    /// Multicast IP group for specific broadcasting
    /// </summary>
    private const string MulticastGroup = "239.0.0.1";

    #endregion

    #region Conection

    /// <summary>
    /// Metodo para detener el servidor TCP
    /// </summary>
    public void StopServer()
    {
        running = false;
        searchingDevices = false;

        foreach (var c in clients.Values)
            try { c.Close(); } catch { }
        clients.Clear();

        try { listener?.Stop(); } catch { }

        listenThread?.Join(500); // cierra el hilo en un plazo de 500ms
        UnityEngine.Debug.Log("[SignalingServer] Servidor TCP detenido");
    }

    /// <summary>
    /// Metodo que inicia el servidor TCP
    /// </summary>
    public void StartServer()
    {
        running = true;
        ipAddress = StreamManagerHost.Instance.GetIP();

        listener = new TcpListener(IPAddress.Parse(ipAddress), listenPort);
        listener.Start();
        listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "TCP Listen" };
        listenThread.Start(); 

        searchingDevices = true;
        StartCoroutine(SendBroadcast()); 
        UnityEngine.Debug.Log("[SignalingServer] Servidor TCP lanzado");
    }

    /// <summary>
    /// Envia mensajes BROADCAST en la red del dispositivo
    /// </summary>
    /// <returns></returns>
    IEnumerator SendBroadcast()
    {
        try
        {
            string json = JsonUtility.ToJson(new ConnectionData(ipAddress, listenPort, ConnectionEvent.BROADCAST));
            byte[] data = Encoding.UTF8.GetBytes(json);

            using (UdpClient sender = new UdpClient())
            {
                sender.Client.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), 0));
                sender.Ttl = 4;
                IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(MulticastGroup), broadcastPort);
                sender.Send(data, data.Length, endpoint);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Host] Error al enviar multicast: {e.Message}");
        }

        yield return new WaitForSeconds(2f);

        if (searchingDevices)
            StartCoroutine(SendBroadcast());
    }

    /// <summary>
    /// Bucle de recepcion de informacion de los clientes
    /// </summary>
    private void ListenLoop()
    {
        while (running)
        {
            try
            {
                TcpClient tcp = listener.AcceptTcpClient();
                Debug.Log($"[Server] TCP connection from: {((IPEndPoint)tcp.Client.RemoteEndPoint).Address}");

                // Each client gets its own thread for reading
                Thread clientThread = new Thread(() => HandleClient(tcp))
                {
                    IsBackground = true,
                    Name = "TCP Client"
                };
                clientThread.Start();
            }
            catch (SocketException)
            {
                // Thrown when listener.Stop() is called � expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalingServer] AcceptLoop error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gestion de los clientes (handshake y bucle de recepcion)
    /// </summary>
    /// <param name="tcp"></param>
    private void HandleClient(TcpClient tcp)
    {
        NetworkStream stream = tcp.GetStream();
        string clientID = "";

        try
        {
            // Handshake ---
            byte[] header = new byte[4];
            if (!TryReadExact(stream, header, 4))
            {
                Debug.LogError("[Signaling Server] Cliente cerró conexión antes del handshake.");
                return;
            }
            int size = BitConverter.ToInt32(header, 0);
            byte[] data = new byte[size];
            ReadExact(stream, data, size);
            string message = Encoding.UTF8.GetString(data);

            ConnectionData decodedData = JsonUtility.FromJson<ConnectionData>(message);

            // Check if the data recieved is truly a ConnectionData class
            if (decodedData.connType != ConnectionEvent.HANDSHAKE)
            {
                Debug.LogError("[Signaling Server] Not a Connection Data recieved during Handshake.");
                return;
            }

            clientID = Guid.NewGuid().ToString();
            ClientData newClient = new ClientData(decodedData, stream, clientID);
            UnityMainThreadDispatcher.Instance().Enqueue(() => StreamManagerHost.Instance?.CreatePeerForClient(newClient));
            clients.TryAdd(clientID, tcp);

            Debug.Log($"[SignalingServer] Client connected: {decodedData.ipAddress}");

            // Data process loop ---
            while (running)
            {
                // Leer header de 4 bytes con el tama�o del mensaje
                header = new byte[4];
                if (!TryReadExact(stream, header, 4)) break;

                size = BitConverter.ToInt32(header, 0);
                byte[] body = new byte[size];
                ReadExact(stream, body, size);

                string incoming = Encoding.UTF8.GetString(body);
                var sigMsg = JsonUtility.FromJson<SignalingMessage>(incoming);

                // Ejecutar en el hilo principal de Unity (los peers WebRTC lo necesitan)
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    StreamManagerHost.Instance?.HandleIncomingSignaling(decodedData.ipAddress, sigMsg));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[Signaling Server] Exception thrown: " + ex.ToString());
            return;
        }
        finally
        {
            clients.TryRemove(clientID, out _);
            tcp.Close();
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                StreamManagerHost.Instance?.RemovePeerForClient(clientID));
        }
    }

    private bool TryReadExact(NetworkStream stream, byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, total, count - total);
            if (read == 0)
            {
                if (total == 0) return false; // cierre limpio, no hay más mensajes
                throw new IOException("Conexión cerrada a mitad de un mensaje.");
            }
            total += read;
        }
        return true;
    }

    private void ReadExact(NetworkStream stream, byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, total, count - total);
            if (read == 0) throw new IOException("Conexión cerrada durante la lectura.");
            total += read;
        }
    }
    #endregion



    #region Monobehaviour
    public void Start()
    {
        bufferSize = 1024;
        ipAddress = StreamManagerHost.Instance.GetIP();
        StartCoroutine(WebRTC.Update());
        StartServer();
    }

    void OnDestroy()
    {
        try { StopServer(); } catch { }
    }

    #endregion
}
