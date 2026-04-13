using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class InputClient : MonoBehaviour
{
    TcpClient client;
    NetworkStream stream;

    void Start()
    {
        client = new TcpClient("127.0.0.1", 8052);
        stream = client.GetStream();
        byte[] buffer = Encoding.UTF8.GetBytes("Conexion establecida");
        stream.Write(buffer, 0, buffer.Length);
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool j = Input.GetButton("Jump");

        string message = $"H:{h},V:{v},J:{(j ? 1 : 0)}";

        byte[] data = Encoding.UTF8.GetBytes(message);
        stream.Write(data, 0, data.Length);
    }

    void OnApplicationQuit()
    {
        stream.Close();
        client.Close();
    }
}