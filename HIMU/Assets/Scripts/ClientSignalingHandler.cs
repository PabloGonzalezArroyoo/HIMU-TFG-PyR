using Assets.Scripts;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;

public class ClientSignalingHandler : MonoBehaviour
{
    public static ClientSignalingHandler Instance { get; private set; }

    private NetworkStream stream;
    private Thread readThread;
    private bool running = false;

    private WebRTCReceiver receiver;

    // Llamado desde ClientConnectionManager.OnConnectionStarted()
    // en lugar de cerrar el TCP tras el handshake
    public void StartSession(NetworkStream tcpStream, string hostIp)
    {
        stream = tcpStream;
        running = true;

        // Crear y configurar el receiver
        var go = new GameObject("WebRTCReceiver");
        receiver = go.AddComponent<WebRTCReceiver>();
        receiver.RemoteIp = hostIp;
        receiver.OnSignalingMessage = SendSignalingMessage;
        receiver.Initialize();

        readThread = new Thread(ReadLoop) { IsBackground = true };
        readThread.Start();
    }

    private void ReadLoop()
    {
        byte[] header = new byte[4];
        while (running)
        {
            try
            {
                // Leer tamaño del mensaje
                int read = stream.Read(header, 0, 4);
                if (read == 0) break;

                int size = BitConverter.ToInt32(header, 0);
                byte[] body = new byte[size];
                int total = 0;
                while (total < size)
                    total += stream.Read(body, total, size - total);

                string json = Encoding.UTF8.GetString(body);
                var msg = JsonUtility.FromJson<SignalingMessage>(json);

                // Despachar al main thread
                UnityMainThreadDispatcher.Instance()?.Enqueue(() => HandleMessage(msg));
            }
            catch (Exception e)
            {
                if (running) Debug.LogWarning($"[ClientSignaling] {e.Message}");
                break;
            }
        }
    }

    private void HandleMessage(SignalingMessage msg)
    {
        if (msg.type == ConnectionEvent.SDP)
        {
            var offer = JsonUtility.FromJson<RTCSessionDescription>(msg.body);
            StartCoroutine(receiver.HandleOffer(offer));
        }
        else if (msg.type == ConnectionEvent.ICE)
        {
            var init = JsonUtility.FromJson<RTCIceCandidateInit>(msg.body);
            receiver.AddIceCandidate(init);
        }
    }

    private void SendSignalingMessage(SignalingMessage msg)
    {
        string json = JsonUtility.ToJson(msg);
        byte[] data = Encoding.UTF8.GetBytes(json);
        byte[] header = BitConverter.GetBytes(data.Length);
        stream.Write(header, 0, 4);
        stream.Write(data, 0, data.Length);
        stream.Flush();
    }

    void Awake()
    {
        if (Instance) { DestroyImmediate(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        running = false;
        stream?.Close();
    }
}