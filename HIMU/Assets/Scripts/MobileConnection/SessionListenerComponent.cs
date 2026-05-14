using Assets.Scripts;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class SessionListenerComponent : MonoBehaviour
{
    [SerializeField] ConnectionUIManager uiManager;
    private UdpClient listener;
    private Thread listenThread;
    private bool running = false;
    private int listenPort = 8053;

    public void StartBroadcast()
    {
        Debug.Log("Lanzando listen loop");

        listener = new UdpClient(listenPort);
        listenThread = new Thread(BroadcastListenLoop) { IsBackground = true };
        listenThread.Start();
    }

    private void BroadcastListenLoop()
    {
        while (running)
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

                uiManager.AddNewSessionUI(decodedData);
            }
            catch (SocketException)
            {
                break; // Socket cerrado
            }
            catch (Exception e)
            {
                if (running)
                {
                    Debug.LogWarning($"[Client] Error en hilo de broadcast: {e.Message}");
                    //HandleDisconnection();
                }
            }
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        running = true;
        StartBroadcast();
    }

    private void OnDestroy()
    {
        running = false;
        listenThread.Abort();
        listenThread = null;
        listener.Close();
    }
}
