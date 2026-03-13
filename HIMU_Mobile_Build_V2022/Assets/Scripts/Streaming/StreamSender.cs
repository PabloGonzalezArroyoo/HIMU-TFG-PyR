using NativeWebSocket;
using System.Threading.Tasks;
using UnityEngine;

public static class StreamSender
{
    static WebSocket websocket;

    [RuntimeInitializeOnLoadMethod]
    static async void Init()
    {
        await Initialize();
    }

    [RuntimeInitializeOnLoadMethod]
    static void CreateUpdater()
    {
        var obj = new GameObject("StreamingUpdater");
        obj.AddComponent<StreamingUpdater>();
        Object.DontDestroyOnLoad(obj);
    }

    public static async Task Initialize()
    {
        websocket = new WebSocket("ws://localhost:8080");

        websocket.OnOpen += () =>
        {
            Debug.Log("WebSocket conectado");
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("WebSocket error: " + e);
        };

        await websocket.Connect();
    }

    public static async void SendFrame(byte[] data)
    {
        if (websocket == null)
            return;

        if (websocket.State == WebSocketState.Open)
        {
            await websocket.Send(data);
        }
    }

    public static void Update()
    {
        websocket?.DispatchMessageQueue();
    }
}