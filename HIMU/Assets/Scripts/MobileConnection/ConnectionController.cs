using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Threading;

public class InputServer : MonoBehaviour
{
    TcpListener server;
    Thread serverThread;
    bool running = true;

    public float horizontal;
    public float vertical;
    public bool jump;

    void Start()
    {
        serverThread = new Thread(StartServer);
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    void StartServer()
    {
        server = new TcpListener(IPAddress.Any, 8052);
        server.Start();

        while (running)
        {
            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[1024];
            int length;

            while ((length = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                string data = Encoding.UTF8.GetString(buffer, 0, length);
                Debug.Log("Informacion recibida desde el movil");
            }

            client.Close();
        }
    }

    void ProcessInput(string data)
    {
        // Ejemplo: "H:0.5,V:-1,J:1"
        string[] parts = data.Split(',');

        foreach (var part in parts)
        {
            if (part.StartsWith("H:"))
                horizontal = float.Parse(part.Substring(2));
            if (part.StartsWith("V:"))
                vertical = float.Parse(part.Substring(2));
            if (part.StartsWith("J:"))
                jump = part.Substring(2) == "1";
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        server.Stop();
    }
}