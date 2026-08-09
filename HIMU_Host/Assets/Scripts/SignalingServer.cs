using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

public class SignalingServer : MonoBehaviour
{

    #region Variables

    /// <summary>
    /// Wether the server is running or not
    /// </summary>
    private bool running;

    /// <summary>
    /// Wether if the server is searching for new devieces or not
    /// </summary>
    private bool searchingDevices;

    /// <summary>
    /// Port where the server will listen to upcoming network data
    /// </summary>
    private int listenPort = 7777;

    /// <summary>
    /// Port from where the broadcast is going to be made
    /// </summary>
    private int broadcastPort = 8053;

    /// <summary>
    /// Listener para escuchar mensajes de los clientes
    /// </summary>
    private TcpListener listener;

    /// <summary>
    /// Hilo de escucha de mensajes
    /// </summary>
    private Thread listenThread;
    
    /// <summary>
    /// Clients structure.
    /// </summary>
    private readonly ConcurrentDictionary<string, TcpClient> tcpSockets = new ConcurrentDictionary<string, TcpClient>();

    /// <summary>
    /// Multicast IP group for specific broadcasting
    /// </summary>
    private const string MulticastGroup = "239.0.0.1";

    private List<ClientData> clients = new List<ClientData>();
    #endregion

    #region Conection

    /// <summary>
    /// Stops the TCP server.
    /// </summary>
    public void StopServer()
    {
        running = false;
        searchingDevices = false;

        foreach (var s in tcpSockets.Values)
            try { s.Close(); } catch { }
        tcpSockets.Clear();

        try { listener?.Stop(); } catch { }

        listenThread?.Join(500); // cierra el hilo en un plazo de 500ms
        Debug.Log("[SignalingServer] TCP server stopped.");
    }

    /// <summary>
    /// Initializes the TCP server.
    /// </summary>
    public void StartServer()
    {
        running = true;

        listener = new TcpListener(IPAddress.Parse(NetworkUtils.GetIP()), listenPort);
        listener.Start();
        listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "TCP Listen" };
        listenThread.Start(); 

        searchingDevices = true;
        StartCoroutine(SendBroadcast()); 
        Debug.Log("[SignalingServer] TCP server launched.");
    }

    /// <summary>
    /// Envia mensajes BROADCAST en la red del dispositivo
    /// </summary>
    /// <returns></returns>
    IEnumerator SendBroadcast()
    {
        try
        {
            string ip = NetworkUtils.GetIP();
            ConnectionData connectionData = ConnectionData.ForBroadcast(ip, listenPort);
            connectionData.sessionName = StreamManager.Instance.sessionName;
            connectionData.sessionID = StreamManager.Instance.sessionID;
            string json = JsonUtility.ToJson(connectionData);
            byte[] data = Encoding.UTF8.GetBytes(json);

            using (UdpClient sender = new UdpClient())
            {
                sender.Client.Bind(new IPEndPoint(IPAddress.Parse(ip), 0));
                sender.Ttl = 4;
                IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(MulticastGroup), broadcastPort);
                sender.Send(data, data.Length, endpoint);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SignalingServer] Error sending multicast: {e.Message}");
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
                Debug.Log($"[SignalingServer] TCP connection from: {((IPEndPoint)tcp.Client.RemoteEndPoint).Address}");

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
    /// Sends the signaling message of this project to the TCP server.
    /// </summary>
    /// <param name="clientID">ID of the client from where the message is going to be sent.</param>
    /// <param name="msg">Signaling message content.</param>
    public bool SendMessage(string clientID, SignalingMessage msg)
    {
        if (!tcpSockets.TryGetValue(clientID, out TcpClient tcp)) return false;

        try
        {
            NetworkStream stream = tcp.GetStream();
            // Se usa el propio stream como objeto de lock: varios hilos de cliente pueden
            // llamar a SendMessage sobre el mismo stream concurrentemente.
            NetworkUtils.WriteFramedMessage(stream, JsonUtility.ToJson(msg), syncRoot: stream);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SignalingServer] Error sending message to {clientID}: {ex.Message}");
            return false;
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
            if (!NetworkUtils.TryReadFramedMessage(stream, out string message))
            {
                Debug.LogError("[SignalingServer] Client clossed connection before handsake.");
                return;
            }

            ConnectionData decodedData = JsonUtility.FromJson<ConnectionData>(message);

            // Check if the data recieved is truly a ConnectionData class
            if (decodedData.connType != ConnectionEvent.HANDSHAKE)
            {
                Debug.LogError("[SignalingServer] Not a Connection Data recieved during handshake.");
                return;
            }

            clientID = Guid.NewGuid().ToString();
            ClientData newClient = ClientData.ForDevice(decodedData, clientID);
            UnityMainThreadDispatcher.Instance().Enqueue(() => StreamManager.Instance?.CreatePeer(newClient));
            clients.Add(newClient);
            tcpSockets.TryAdd(clientID, tcp);

            Debug.Log($"[SignalingServer] Client connected: {decodedData.ipAddress}");
            UIManager.Instance?.UpdateTCPClientsText(true);

            // Data process loop ---
            while (running)
            {
                if (!NetworkUtils.TryReadFramedMessage(stream, out string incoming)) break;

                var sigMsg = JsonUtility.FromJson<SignalingMessage>(incoming);

                // Ejecutar en el hilo principal de Unity (los peers WebRTC lo necesitan)
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    StreamManager.Instance?.HandleIncomingSignaling(clientID, sigMsg));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[SignalingServer] Exception thrown: " + ex.ToString());
            return;
        }
        finally
        {
            tcpSockets.TryRemove(clientID, out _);
            tcp.Close();
            UnityMainThreadDispatcher.Instance().Enqueue(() => 
                StreamManager.Instance?.RemovePeer(clientID));
            UIManager.Instance?.UpdateTCPClientsText(false);
        }
    }

    #endregion

    public List<ClientData> GetClients()
    {
        return clients;
    }

    #region Monobehaviour

    void OnDestroy()
    {
        if (!running) return;
        try { StopServer(); } catch { }
    }

    #endregion
}
