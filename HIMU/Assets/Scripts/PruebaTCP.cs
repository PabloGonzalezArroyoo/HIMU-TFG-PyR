using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Assets.Scripts;
using UnityEngine.UI;

public class PruebaTCP : MonoBehaviour
{
    private UdpClient listener;
    private Thread listenThread;
    private Thread receiveThread;           // [NUEVO] Hilo de recepción de textura

    private bool running = false;
    private bool connected = false;

    private int listenPort = 8053;
    private int hostPort;

    private string hostIP;
    private string ipAddress;

    // [NUEVO] Referencia a la RenderTexture donde se vuelca la imagen recibida
    public RenderTexture targetRenderTexture;

    // [NUEVO] Buffer compartido entre hilo de red y hilo principal
    private byte[] pendingFrameData = null;
    private bool hasNewFrame = false;
    private readonly object frameLock = new object();

    // [NUEVO] Conexión TCP reutilizada para recibir frames
    private TcpClient tcpConnection;
    private NetworkStream tcpStream;

    public RawImage rawImage;

    public void ButtonStartBroadcast()
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

                hostIP = decodedData.ipAddress;
                hostPort = decodedData.port;
                connected = true;
                Debug.Log($"[Client] Host encontrado: {hostIP} — enviando respuesta…");

                string json = JsonUtility.ToJson(new ConnectionData(ipAddress, listenPort, ConnectionEvent.HANDSHAKE));
                byte[] responseData = Encoding.UTF8.GetBytes(json);

                // [MODIFICADO] Guardamos la conexión TCP para reutilizarla
                tcpConnection = new TcpClient();
                tcpConnection.Connect(hostIP, hostPort);
                tcpStream = tcpConnection.GetStream();
                tcpStream.Write(responseData, 0, responseData.Length);
                Debug.Log($"[Client] Handshake enviado a {hostIP}:{hostPort}");

                // [NUEVO] Iniciamos el hilo de recepción de frames
                receiveThread = new Thread(ReceiveFrameLoop) { IsBackground = true };
                receiveThread.Start();
            }
            catch (SocketException)
            {
                break;
            }
            catch (Exception e)
            {
                if (running)
                    Debug.LogWarning($"[Client] Error en hilo de broadcast: {e.Message}");
            }
        }
    }

    // [NUEVO] Lee frames continuamente desde el stream TCP
    private void ReceiveFrameLoop()
    {
        byte[] header = new byte[4];

        while (running && connected)
        {
            try
            {
                // 1. Leer los 4 bytes del header que indican el tamaño del frame
                if (!ReadExact(tcpStream, header, 4))
                {
                    Debug.LogWarning("[Client] Conexión cerrada por el host.");
                    break;
                }

                Debug.Log("Leyendo frame");

                int frameSize = BitConverter.ToInt32(header, 0);

                if (frameSize <= 0 || frameSize > 20_000_000) // Límite de seguridad: 20 MB
                {
                    Debug.LogWarning($"[Client] Tamaño de frame inválido: {frameSize}");
                    break;
                }

                // 2. Leer exactamente frameSize bytes
                byte[] frameData = new byte[frameSize];
                if (!ReadExact(tcpStream, frameData, frameSize))
                {
                    Debug.LogWarning("[Client] Stream cortado durante la lectura del frame.");
                    break;
                }

                // 3. Entregar al hilo principal de forma segura
                lock (frameLock)
                {
                    pendingFrameData = frameData;
                    hasNewFrame = true;
                }
            }
            catch (Exception e)
            {
                if (running)
                    Debug.LogWarning($"[Client] Error recibiendo frame: {e.Message}");
                break;
            }
        }
    }

    // [NUEVO] Lectura garantizada de exactamente 'count' bytes
    private bool ReadExact(NetworkStream stream, byte[] buffer, int count)
    {
        int received = 0;
        while (received < count)
        {
            int n = stream.Read(buffer, received, count - received);
            if (n == 0) return false; // Conexión cerrada
            received += n;
        }
        return true;
    }

    // [NUEVO] Aplica el frame recibido a la RenderTexture en el hilo principal
    private void Update()
    {
        if (!hasNewFrame) return;

        byte[] data;
        lock (frameLock)
        {
            data = pendingFrameData;
            hasNewFrame = false;
        }

        Texture2D tex = new Texture2D(2, 2);
        if (tex.LoadImage(data)) // Acepta JPG y PNG automáticamente
        {
            if (targetRenderTexture == null)
            {
                // Si no hay RenderTexture asignada, la creamos con el tamaño del frame
                targetRenderTexture = new RenderTexture(tex.width, tex.height, 0);
            }

            Graphics.Blit(tex, targetRenderTexture);
            rawImage.texture = targetRenderTexture;
            Debug.Log("Imagen aplicada");
        }
        else
        {
            Debug.LogWarning("[Client] No se pudo decodificar el frame recibido.");
        }

        Destroy(tex);
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

    private void OnDestroy()
    {
        running = false;
        listener?.Close();
        tcpStream?.Close();
        tcpConnection?.Close();
    }

    private void Start() { }
}