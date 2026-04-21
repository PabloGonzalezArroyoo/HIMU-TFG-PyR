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
    protected Image backgroundImage;
    [SerializeField]
    protected List<GameObject> textsToHide;
    [SerializeField]
    protected float timeToFade = 2.5f;
    protected float timeToHideText = 2f;
    protected float timer = 0f;
    protected bool isFading = false;
    protected bool isHidingText = false;

    // Info general
    [SerializeField]
    protected ConnectionType connectionType = ConnectionType.USB;
    [SerializeField]
    protected bool isGamePad = true;

    protected DeviceInfo deviceIdentifier;
    private bool connected = false;

    // Config ADB
    TcpClient client;
    NetworkStream stream;


    public void ConnectTCP(string ip, int port)
    {
        client = new TcpClient(ip, port);
        stream = client.GetStream();
        byte[] buffer = Encoding.UTF8.GetBytes("Conexion establecida");
        stream.Write(buffer, 0, buffer.Length);
        connected = true;
        isHidingText = true;
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
            ConnectTCP(ip, port);
            connected = true;
            isHidingText = true;
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
        // 1. Serializar el struct a JSON y luego a bytes
        string json = JsonUtility.ToJson(datos);
        byte[] buffer = Encoding.UTF8.GetBytes(json);

        // 2. Enviar primero la longitud (4 bytes) y luego el contenido
        byte[] longitudBytes = BitConverter.GetBytes(buffer.Length);
        await stream.WriteAsync(longitudBytes, 0, longitudBytes.Length);
        await stream.WriteAsync(buffer, 0, buffer.Length);
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

        if (connectionType == ConnectionType.USB)
        {
            ConnectTCP("127.0.0.1", 8052);
        }
        else
        {
            StartCoroutine(DiscoverAndConnect());
        }
    }

    void OnApplicationQuit()
    {
        stream.Close();
        client.Close();
    }


    private void Update()
    {
        if (connected && isHidingText)
        {
            timer += Time.deltaTime;
            if (timer >= timeToHideText)
            {
                foreach(GameObject g in textsToHide)
                {
                    g.SetActive(false);
                }
                timer = 0f;
                isHidingText = false;
                isFading = true;
            }
        }
        if (connected && isFading)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - timer / timeToFade);
            Color c = backgroundImage.color;
            c.a = alpha;
            backgroundImage.color = c;

            if (timer >= timeToFade)
            {
                timer = 0f;
                isFading = false;
            }
        }
    }
}
