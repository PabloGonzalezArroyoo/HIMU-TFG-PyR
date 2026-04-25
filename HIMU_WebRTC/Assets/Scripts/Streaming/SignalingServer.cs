using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Embedded WebSocket signaling server that runs inside Unity. Replaces the external Node.js
/// server entirely. Accepts connections from any type of client (browsers, mobile Unity instances,
/// desktop apps...) and routes signaling messages between each client and the host peer connection.
///
/// Architecture:
///   - Listens on a TCP port using TcpListener (no external dependencies needed)
///   - Performs the WebSocket handshake manually (RFC 6455)
///   - Each accepted client gets a unique ID and its own send queue
///   - Incoming messages are placed in a thread-safe queue consumed on Unity's main thread
///   - Outgoing messages are sent asynchronously from a dedicated send thread per client
/// </summary>
public class SignalingServer
{
    #region Types

    /// <summary>
    /// Represents a connected client with its own communication channel.
    /// </summary>
    public class Client
    {
        /// <summary>Unique identifier assigned on connection.</summary>
        public string Id { get; }

        /// <summary>Underlying TCP stream for this client.</summary>
        public NetworkStream Stream { get; }

        /// <summary>Thread-safe queue of raw frames waiting to be sent to this client.</summary>
        public ConcurrentQueue<byte[]> SendQueue { get; } = new ConcurrentQueue<byte[]>();

        /// <summary>Whether this client's WebSocket connection is still open.</summary>
        public bool IsAlive { get; set; } = true;

        public Client(string id, NetworkStream stream)
        {
            Id = id;
            Stream = stream;
        }
    }

    /// <summary>
    /// A signaling message received from a client, queued for processing on Unity's main thread.
    /// </summary>
    public struct IncomingMessage
    {
        /// <summary>ID of the client that sent the message.</summary>
        public string ClientId;

        /// <summary>Raw JSON text of the message.</summary>
        public string Json;
    }

    #endregion

    #region Public API

    /// <summary>Port the server listens on. Configurable in the Inspector via WebRTCStreamer.</summary>
    public int Port { get; }

    /// <summary>
    /// Thread-safe queue of messages received from clients, ready to be consumed
    /// on Unity's main thread each frame.
    /// </summary>
    public ConcurrentQueue<IncomingMessage> IncomingMessages { get; } = new ConcurrentQueue<IncomingMessage>();

    /// <summary>Raised on the main thread (via polling) when a new client connects.</summary>
    public event Action<string> OnClientConnected;

    /// <summary>Raised on the main thread (via polling) when a client disconnects.</summary>
    public event Action<string> OnClientDisconnected;

    #endregion

    #region Private state

    TcpListener listener;

    /// <summary>
    /// All currently connected clients, keyed by their ID.
    /// ConcurrentDictionary is used instead of Dictionary + lock because multiple background
    /// threads (one per client) may add or remove entries simultaneously. All individual
    /// operations (TryAdd, TryRemove, TryGetValue) are atomic, and its enumerator works on
    /// a snapshot so Broadcast iteration is safe without an external lock.
    /// </summary>
    readonly ConcurrentDictionary<string, Client> clients = new ConcurrentDictionary<string, Client>();

    /// <summary>Counter used to generate sequential client IDs.</summary>
    int nextClientId = 1;

    /// <summary>Set to false to stop all server threads.</summary>
    bool running = false;

    // Events that need to fire on the main thread are enqueued here.
    readonly ConcurrentQueue<Action> mainThreadEvents = new ConcurrentQueue<Action>();

    #endregion

    #region Lifecycle

    public SignalingServer(int port)
    {
        Port = port;
    }

    /// <summary>
    /// Starts the TCP listener and begins accepting clients on a background thread.
    /// Safe to call from Unity's main thread.
    /// </summary>
    public void Start()
    {
        running = true; 
        listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        Debug.Log($"[SignalingServer] Listening on port {Port}");

        // Accept loop runs on its own thread so it never blocks Unity
        Thread acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "WS-Accept" };
        acceptThread.Start();
    }

    /// <summary>
    /// Stops the server and disconnects all clients. Call from OnDestroy.
    /// </summary>
    public void Stop()
    {
        running = false;
        listener?.Stop();

        // ConcurrentDictionary enumerator works on a snapshot — safe to iterate and mutate
        foreach (var client in clients.Values)
        {
            client.IsAlive = false;
            client.Stream?.Close();
        }
        clients.Clear();

        Debug.Log("[SignalingServer] Stopped");
    }

    /// <summary>
    /// Must be called once per frame from Unity's main thread (e.g. from Update).
    /// Fires pending connect/disconnect events so Unity code can react on the main thread.
    /// </summary>
    public void Tick()
    {
        while (mainThreadEvents.TryDequeue(out Action action))
            action?.Invoke();
    }

    #endregion

    #region Send

    /// <summary>
    /// Sends a JSON message to a specific client identified by its ID.
    /// Thread-safe; can be called from any thread.
    /// </summary>
    public void SendTo(string clientId, string json)
    {
        if (!clients.TryGetValue(clientId, out Client client)) return;

        if (!client.IsAlive) return;

        byte[] frame = BuildTextFrame(json);
        client.SendQueue.Enqueue(frame);
    }

    /// <summary>
    /// Broadcasts a JSON message to every connected client except the one specified.
    /// Pass null as excludeId to broadcast to everyone.
    /// </summary>
    public void Broadcast(string json, string excludeId = null)
    {
        byte[] frame = BuildTextFrame(json);

        foreach (var kv in clients)
        {
            if (kv.Key == excludeId || !kv.Value.IsAlive) continue;
            kv.Value.SendQueue.Enqueue(frame);
        }
    }

    #endregion

    #region Accept loop (background thread)

    void AcceptLoop()
    {
        while (running)
        {
            try
            {
                TcpClient tcp = listener.AcceptTcpClient();

                // Each client gets its own thread for reading
                Thread clientThread = new Thread(() => HandleClient(tcp))
                {
                    IsBackground = true,
                    Name = "WS-Client"
                };
                clientThread.Start();
            }
            catch (SocketException)
            {
                // Thrown when listener.Stop() is called — expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SignalingServer] AcceptLoop error: {ex.Message}");
            }
        }
    }

    #endregion

    #region Per-client handling (background thread)

    void HandleClient(TcpClient tcp)
    {
        NetworkStream stream = tcp.GetStream();
        string clientId = null;

        try
        {
            // --- WebSocket handshake ---
            if (!PerformHandshake(stream))
            {
                Debug.LogWarning("[SignalingServer] Handshake failed, dropping client");
                tcp.Close();
                return;
            }

            // Register client
            clientId = $"client_{nextClientId++}";
            var client = new Client(clientId, stream);

            clients.TryAdd(clientId, client);

            Debug.Log($"[SignalingServer] Client connected: {clientId}");

            // Notify main thread
            string capturedId = clientId;
            mainThreadEvents.Enqueue(() => OnClientConnected?.Invoke(capturedId));

            // Start a dedicated send thread for this client
            Thread sendThread = new Thread(() => SendLoop(client))
            {
                IsBackground = true,
                Name = $"WS-Send-{clientId}"
            };
            sendThread.Start();

            // --- Receive loop ---
            while (running && client.IsAlive)
            {
                string json = ReadFrame(stream);

                // null means the connection was closed cleanly or an error occurred
                if (json == null) break;

                IncomingMessages.Enqueue(new IncomingMessage { ClientId = clientId, Json = json });
            }
        }
        catch (Exception ex)
        {
            // Only log if we were actually running (not a clean shutdown)
            if (running)
                Debug.LogWarning($"[SignalingServer] Client {clientId} error: {ex.Message}");
        }
        finally
        {
            if (clientId != null)
            {
                // TryRemove is atomic: removes the entry and returns the client in one operation,
                // eliminating any race condition between lookup and deletion
                if (clients.TryRemove(clientId, out var c))
                    c.IsAlive = false;

                Debug.Log($"[SignalingServer] Client disconnected: {clientId}");

                string capturedId = clientId;
                mainThreadEvents.Enqueue(() => OnClientDisconnected?.Invoke(capturedId));
            }

            try { stream?.Close(); } catch { /* ignored */ }
            try { tcp?.Close(); } catch { /* ignored */ }
        }
    }

    /// <summary>
    /// Dedicated send loop for one client. Drains its SendQueue as fast as possible
    /// without blocking the receive thread.
    /// </summary>
    void SendLoop(Client client)
    {
        while (running && client.IsAlive)
        {
            try
            {
                if (client.SendQueue.TryDequeue(out byte[] frame))
                {
                    client.Stream.Write(frame, 0, frame.Length);
                }
                else
                {
                    // Nothing to send — yield the thread briefly to avoid a busy-wait spin
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                if (running)
                    Debug.LogWarning($"[SignalingServer] SendLoop error for {client.Id}: {ex.Message}");
                client.IsAlive = false;
                break;
            }
        }
    }

    #endregion

    #region WebSocket RFC 6455 implementation

    /// <summary>
    /// Reads the HTTP upgrade request and responds with a valid WebSocket handshake.
    /// Returns true if the handshake was completed successfully.
    /// </summary>
    bool PerformHandshake(NetworkStream stream)
    {
        // Read the HTTP request line by line until we hit the blank line
        var sb = new StringBuilder();
        var buffer = new byte[1];
        string key = null;

        while (true)
        {
            // Read one line
            var line = new StringBuilder();
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1) return false;

                char c = (char)b;
                if (c == '\n') break;
                if (c != '\r') line.Append(c);
            }

            string lineStr = line.ToString();

            // Blank line = end of headers
            if (lineStr.Length == 0) break;

            // Extract the Sec-WebSocket-Key header
            if (lineStr.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                key = lineStr.Substring("Sec-WebSocket-Key:".Length).Trim();
        }

        if (string.IsNullOrEmpty(key))
            return false;

        // Compute the accept key (RFC 6455 §1.3)
        string acceptKey = Convert.ToBase64String(
            SHA1.Create().ComputeHash(
                Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
            )
        );

        // Send the 101 Switching Protocols response
        string response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        stream.Write(responseBytes, 0, responseBytes.Length);
        return true;
    }


    /// <summary>
    /// Reads one WebSocket frame from the stream and returns its payload as a UTF-8 string.
    /// Returns null if the connection was closed or an unrecoverable error occurred.
    /// Only handles text frames (opcode 0x1) and close frames (opcode 0x8).
    /// </summary>
    string ReadFrame(NetworkStream stream)
    {
        // Byte 0: FIN bit + opcode
        int b0 = stream.ReadByte();
        if (b0 == -1) return null;

        int opcode = b0 & 0x0F;

        // Connection close frame
        if (opcode == 0x8) return null;

        // Byte 1: MASK bit + payload length (7-bit)
        int b1 = stream.ReadByte();
        if (b1 == -1) return null;

        bool masked = (b1 & 0x80) != 0;
        long payloadLen = b1 & 0x7F;

        // Extended payload lengths (RFC 6455 §5.2)
        if (payloadLen == 126)
        {
            byte[] ext = ReadBytes(stream, 2);
            if (ext == null) return null;
            payloadLen = (ext[0] << 8) | ext[1];
        }
        else if (payloadLen == 127)
        {
            byte[] ext = ReadBytes(stream, 8);
            if (ext == null) return null;
            payloadLen = BitConverter.ToInt64(ext, 0);
        }

        // Masking key (clients MUST mask; servers MUST NOT)
        byte[] maskKey = null;
        if (masked)
        {
            maskKey = ReadBytes(stream, 4);
            if (maskKey == null) return null;
        }

        // Payload
        byte[] payload = ReadBytes(stream, (int)payloadLen);
        if (payload == null) return null;

        // Unmask payload if needed
        if (masked && maskKey != null)
        {
            for (int i = 0; i < payload.Length; i++)
                payload[i] ^= maskKey[i % 4];
        }

        return Encoding.UTF8.GetString(payload);
    }


    /// <summary>
    /// Builds an unmasked WebSocket text frame for the given UTF-8 string.
    /// Servers send unmasked frames (RFC 6455 §5.1).
    /// </summary>
    byte[] BuildTextFrame(string text)
    {
        byte[] payload = Encoding.UTF8.GetBytes(text);
        int len = payload.Length;

        // Frame header: FIN=1, opcode=0x1 (text)
        using var ms = new MemoryStream();

        ms.WriteByte(0x81); // FIN + text opcode

        if (len <= 125)
        {
            ms.WriteByte((byte)len);
        }
        else if (len <= 65535)
        {
            ms.WriteByte(126);
            ms.WriteByte((byte)(len >> 8));
            ms.WriteByte((byte)(len & 0xFF));
        }
        else
        {
            ms.WriteByte(127);
            byte[] lenBytes = BitConverter.GetBytes((long)len);
            // Big-endian
            for (int i = 7; i >= 0; i--) ms.WriteByte(lenBytes[i]);
        }

        ms.Write(payload, 0, payload.Length);
        return ms.ToArray();
    }


    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes from the stream.
    /// Returns null if the stream closes before all bytes arrive.
    /// </summary>
    byte[] ReadBytes(NetworkStream stream, int count)
    {
        byte[] buf = new byte[count];
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buf, read, count - read);
            if (n == 0) return null;
            read += n;
        }
        return buf;
    }

    #endregion
}
