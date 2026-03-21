using UnityEngine;
using System.Net.Sockets;
using System.Text;

public class UDPInputClient : MonoBehaviour
{
    public string serverIP = "192.168.1.100"; // IP del PC
    public int port = 7777;

    private UdpClient client;
    private MobileInputData inputData = new MobileInputData();

    void Start()
    {
        client = new UdpClient();
    }

    void Update()
    {
        // Movimiento táctil simple
        inputData.horizontal = Input.acceleration.x;
        inputData.vertical = Input.acceleration.y;

        inputData.jump = Input.touchCount > 0;

        SendData();
    }

    void SendData()
    {
        string json = JsonUtility.ToJson(inputData);
        byte[] data = Encoding.UTF8.GetBytes(json);

        client.Send(data, data.Length, serverIP, port);
    }
}