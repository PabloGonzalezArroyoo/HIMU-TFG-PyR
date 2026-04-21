using Assets.Scripts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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

    protected DeviceInfo deviceIdentifier;
    private bool connected = false;

    // Config UDP
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;

    public void ConnectUDP(string ip, int port)
    {
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        udpClient = new UdpClient();
        udpClient.Connect(remoteEndPoint);

        byte[] buffer = Encoding.UTF8.GetBytes("Conexion establecida");
        udpClient.Send(buffer, buffer.Length);

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

    #region Getters/Setters
    private DeviceInfo CreateDeviceIdentifier()
    {
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
        return new DeviceInfo(uid, ipaddress);
    }

    public DeviceInfo GetDeviceInfo()
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
        if (udpClient == null || !connected)
        {
            Debug.LogWarning("[UDP] No hay conexion activa.");
            return;
        }

        // Serializar el struct a JSON y luego a bytes
        // UDP es orientado a datagramas: no se necesita enviar la longitud por separado,
        // cada Send() es un datagrama completo e independiente.
        string json = JsonUtility.ToJson(datos);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        await udpClient.SendAsync(buffer, buffer.Length);
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

        Debug.Log(deviceIdentifier.deviceIP);


        if (connectionType == ConnectionType.USB)
        {
            ConnectUDP("127.0.0.1", 8052);
        }
        else
        {
            StartCoroutine(DiscoverAndConnect());
        }
    }

    void OnApplicationQuit()
    {
        udpClient?.Close();
        udpClient = null;
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
}
