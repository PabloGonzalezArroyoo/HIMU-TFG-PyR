using Assets.Scripts;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ComunicationManager : MonoBehaviour
{
    [SerializeField] private int listenPort = 8053;
    private TcpClient TCPClient;
    private NetworkStream stream;
    private string hostIPAddress = string.Empty;
    public static string ipAddress { get; private set; }

    public static ComunicationManager Instance { get; private set; }

    public NetworkStream GetTCPStream()
    {
        return stream;
    }

    public string GetHostIP()
    {
        return hostIPAddress;
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

    public bool TryTCPConnection(string hostIP, int hostPort)
    {
        Debug.Log($"[Client] Host encontrado: {hostIP} — enviando respuesta…");

        try
        {
            string json = JsonUtility.ToJson(new ConnectionData(ipAddress, listenPort, ConnectionEvent.HANDSHAKE));
            byte[] responseData = Encoding.UTF8.GetBytes(json);

            TCPClient = new TcpClient();

            if (!TCPClient.ConnectAsync(hostIP, hostPort).Wait(TimeSpan.FromSeconds(2)))
            {
                Debug.LogError($"[Client] Timeout al conectar con {hostIP}:{hostPort}");
                TCPClient.Close();
                return false;
            }

            stream = TCPClient.GetStream();
            stream.Write(responseData, 0, responseData.Length);

            Debug.Log($"[Client] Handshake enviado a {hostIP}:{hostPort}");
            hostIPAddress = hostIP;
            return true;
        }
        catch (SocketException ex)
        {
            Debug.LogError($"[Client] Error de socket al conectar con {hostIP}:{hostPort} — {ex.SocketErrorCode}: {ex.Message}");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[Client] Error de I/O al escribir en el stream — {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Client] Error inesperado en TryTCPConnection — {ex.Message}");
        }
        finally
        {
            //if (TCPClient != null && !TCPClient.Connected)
            //{
            //    TCPClient.Close();
            //    TCPClient = null;
            //}
        }

        return false;
    }

    void Awake()
    {
        if (Instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(Instance);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GetIpAddress();
    }
}
