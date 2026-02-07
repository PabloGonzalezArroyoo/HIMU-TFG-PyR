using UnityEngine;
using Fleck;

public class SimpleServer : MonoBehaviour
{
    WebSocketServer server;

    void Start()
    {
        server = new WebSocketServer("ws://192.168.1.21:8080");


        server.Start(socket =>
        {
            socket.OnOpen = () => Debug.Log("Nuevo dispositivo cliente conectado");

            socket.OnClose = () => Debug.Log("Dispositivo cliente desconectado");

            socket.OnMessage = message =>
            {
                Debug.Log("Movil dice: " + message);

                // responder al móvil
                socket.Send("Unity dice: " + message);
            };
        });

        Debug.Log("Servidor WebSocket iniciado en puerto 8080");
    }
}
