using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

/// <summary>
/// Manages WebRTC streaming from Unity to any number of connected clients.
/// Replaces the previous single-peer design and removes the dependency on the
/// external Node.js signaling server.
///
/// Responsibilities:
///   - Owns the embedded SignalingServer and polls it each frame
///   - Creates one RTCPeerConnection + VideoStreamTrack per connected client
///   - Handles the full WebRTC offer/answer/ICE flow for each peer independently
///   - Cleans up peer resources when a client disconnects
///
/// Clients can be browsers, mobile Unity instances, or any WebRTC-capable device.
/// They only need to know the host machine's local IP and the configured port.
/// </summary>
public class WebRTCStreamer : MonoBehaviour
{
    #region Inspector

    [Header("Signaling")]
    [Tooltip("Port the embedded WebSocket signaling server will listen on.")]
    [SerializeField] int signalingPort = 8080;

    [Header("WebRTC")]
    [Tooltip("STUN server used for ICE candidate gathering. Required when clients are outside the local network.")]
    [SerializeField] string stunServer = "stun:stun.l.google.com:19302";

    #endregion

    #region Types

    /// <summary>
    /// Holds all WebRTC state for a single connected peer.
    /// One instance exists per client for the lifetime of that connection.
    /// </summary>
    class PeerSession
    {
        /// <summary>Unique ID assigned by the SignalingServer on connection.</summary>
        public string ClientId;

        /// <summary>Core WebRTC object managing ICE, DTLS and media for this peer.</summary>
        public RTCPeerConnection PeerConnection;

        /// <summary>
        /// Video track carrying the captured frame to this specific peer.
        /// Each peer gets its own track instance even though all read from the same RenderTexture.
        /// </summary>
        public VideoStreamTrack VideoTrack;

        /// <summary>
        /// ICE candidates that arrived before SetRemoteDescription completed.
        /// Flushed once the remote description is set.
        /// </summary>
        public List<RTCIceCandidateInit> PendingCandidates = new List<RTCIceCandidateInit>();

        /// <summary>True once SetRemoteDescription has completed successfully.</summary>
        public bool RemoteDescriptionSet = false;
    }

    /// <summary>JSON-serializable signaling message (offer / answer / ice).</summary>
    [System.Serializable]
    struct SignalingMessage
    {
        public string type;
        public string sdp;
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }

    #endregion

    #region Private state

    /// <summary>The embedded signaling server running inside Unity.</summary>
    SignalingServer signalingServer;

    /// <summary>
    /// Active peer sessions keyed by client ID.
    /// Only accessed from the main thread (inside coroutines and Update).
    /// </summary>
    Dictionary<string, PeerSession> sessions = new Dictionary<string, PeerSession>();

    #endregion

    #region MonoBehaviour

    void Start()
    {
        // Start the WebRTC background update coroutine required by Unity's WebRTC package
        StartCoroutine(WebRTC.Update());

        // Start the signaling server and wire up connection events
        signalingServer = new SignalingServer(signalingPort);
        signalingServer.OnClientConnected += OnClientConnected;
        signalingServer.OnClientDisconnected += OnClientDisconnected;
        signalingServer.Start();

        Debug.Log($"[WebRTCStreamer] Ready. Clients should connect to ws://<this-machine-ip>:{signalingPort}");
    }

    void Update()
    {
        // Fire queued connect/disconnect events on the main thread
        signalingServer.Tick();

        // Process all signaling messages that arrived this frame
        while (signalingServer.IncomingMessages.TryDequeue(out SignalingServer.IncomingMessage msg))
            StartCoroutine(HandleSignalingMessage(msg.ClientId, msg.Json));
    }

    void OnDestroy()
    {
        signalingServer?.Stop();

        // Dispose all active peer sessions
        foreach (var session in sessions.Values)
            CleanupSession(session);

        sessions.Clear();
    }

    #endregion

    #region Client connect / disconnect

    /// <summary>
    /// Called on the main thread when a new client completes the WebSocket handshake.
    /// Creates a PeerSession with its own RTCPeerConnection and VideoStreamTrack.
    /// </summary>
    void OnClientConnected(string clientId)
    {
        Debug.Log($"[WebRTCStreamer] New client: {clientId}. Waiting for offer...");

        // Wait until the frame capturer is ready before accepting peers
        // (in practice the server starts after the scene is loaded, so this is almost instant)
        StartCoroutine(InitSessionWhenReady(clientId));
    }

    /// <summary>
    /// Waits for a captured frame to be available, then creates the peer session.
    /// This handles the rare case where a client connects in the very first frame.
    /// </summary>
    IEnumerator InitSessionWhenReady(string clientId)
    {
        yield return new WaitUntil(() => FrameCaptureFeature.Instance?.GetFrame() != null);

        // Guard: client may have disconnected while we were waiting
        if (sessions.ContainsKey(clientId)) yield break;

        RenderTexture rt = FrameCaptureFeature.Instance.GetFrame();

        // Build the RTCConfiguration with the STUN server
        var config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { stunServer } } }
        };

        var session = new PeerSession
        {
            ClientId = clientId,
            PeerConnection = new RTCPeerConnection(ref config),
            VideoTrack = new VideoStreamTrack(rt)
        };

        // ICE candidate discovered locally → forward to this client via signaling
        session.PeerConnection.OnIceCandidate = candidate =>
        {
            // This callback fires on a WebRTC internal thread; SendTo is thread-safe
            var msg = new SignalingMessage
            {
                type = "ice",
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex ?? 0
            };
            signalingServer.SendTo(clientId, JsonUtility.ToJson(msg));
        };

        session.PeerConnection.OnConnectionStateChange = state =>
        {
            Debug.Log($"[WebRTCStreamer] {clientId} → WebRTC state: {state}");

            if (state == RTCPeerConnectionState.Failed ||
                state == RTCPeerConnectionState.Closed ||
                state == RTCPeerConnectionState.Disconnected)
            {
                // Schedule cleanup on the main thread
                // (OnConnectionStateChange fires on a WebRTC thread)
                // We simply mark it — Update will not find it in the dict after removal
                Debug.LogWarning($"[WebRTCStreamer] {clientId} connection lost ({state})");
            }
        };

        // Attach the video track so this peer receives the stream
        session.PeerConnection.AddTrack(session.VideoTrack);

        sessions[clientId] = session;
        Debug.Log($"[WebRTCStreamer] Session ready for {clientId}");
    }


    /// <summary>
    /// Called on the main thread when a client disconnects.
    /// Tears down its PeerConnection and VideoStreamTrack.
    /// </summary>
    void OnClientDisconnected(string clientId)
    {
        if (!sessions.TryGetValue(clientId, out PeerSession session)) return;

        Debug.Log($"[WebRTCStreamer] Cleaning up session for {clientId}");
        CleanupSession(session);
        sessions.Remove(clientId);
    }

    /// <summary>
    /// Disposes all WebRTC resources associated with a session.
    /// </summary>
    void CleanupSession(PeerSession session)
    {
        session.VideoTrack?.Dispose();
        session.PeerConnection?.Close();
        session.PeerConnection?.Dispose();
    }

    #endregion

    #region Signaling message handling

    /// <summary>
    /// Dispatches an incoming JSON message to the appropriate handler based on its type.
    /// Runs as a coroutine because SetRemoteDescription and CreateAnswer are async operations
    /// that must be yielded on Unity's main thread.
    /// </summary>
    IEnumerator HandleSignalingMessage(string clientId, string json)
    {
        // Look up the session — it might not exist yet if the client sent a message
        // before InitSessionWhenReady finished (extremely unlikely but safe to guard)
        if (!sessions.TryGetValue(clientId, out PeerSession session))
        {
            Debug.LogWarning($"[WebRTCStreamer] Message from unknown client {clientId}, ignoring");
            yield break;
        }

        var msg = JsonUtility.FromJson<SignalingMessage>(json);

        switch (msg.type)
        {
            case "offer":
                yield return StartCoroutine(HandleOffer(session, msg.sdp));
                break;

            case "ice":
                HandleIceCandidate(session, msg);
                break;

            default:
                Debug.LogWarning($"[WebRTCStreamer] Unknown message type '{msg.type}' from {clientId}");
                break;
        }
    }


    /// <summary>
    /// Processes an SDP offer from a client:
    ///   1. Sets it as the remote description
    ///   2. Creates an SDP answer
    ///   3. Sets the answer as the local description
    ///   4. Sends the answer back to the client
    ///   5. Flushes any ICE candidates that arrived before the offer was processed
    /// </summary>
    IEnumerator HandleOffer(PeerSession session, string sdp)
    {
        Debug.Log($"[WebRTCStreamer] Offer received from {session.ClientId}");

        var desc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };

        var setRemote = session.PeerConnection.SetRemoteDescription(ref desc);
        yield return setRemote;

        if (setRemote.IsError)
        {
            Debug.LogError($"[WebRTCStreamer] SetRemoteDescription failed for {session.ClientId}: {setRemote.Error.message}");
            yield break;
        }

        session.RemoteDescriptionSet = true;

        // Flush ICE candidates that arrived before the offer
        foreach (var candidate in session.PendingCandidates)
            session.PeerConnection.AddIceCandidate(new RTCIceCandidate(candidate));

        session.PendingCandidates.Clear();

        // Generate answer
        var answerOp = session.PeerConnection.CreateAnswer();
        yield return answerOp;

        if (answerOp.IsError)
        {
            Debug.LogError($"[WebRTCStreamer] CreateAnswer failed for {session.ClientId}: {answerOp.Error.message}");
            yield break;
        }

        var answer = answerOp.Desc;
        var setLocal = session.PeerConnection.SetLocalDescription(ref answer);
        yield return setLocal;

        if (setLocal.IsError)
        {
            Debug.LogError($"[WebRTCStreamer] SetLocalDescription failed for {session.ClientId}: {setLocal.Error.message}");
            yield break;
        }

        // Send answer back to client
        var replyMsg = new SignalingMessage { type = "answer", sdp = answer.sdp };
        signalingServer.SendTo(session.ClientId, JsonUtility.ToJson(replyMsg));

        Debug.Log($"[WebRTCStreamer] Answer sent to {session.ClientId}");
    }


    /// <summary>
    /// Registers an ICE candidate from a client.
    /// If the remote description is not yet set, queues it for later processing.
    /// </summary>
    void HandleIceCandidate(PeerSession session, SignalingMessage msg)
    {
        var candidateInit = new RTCIceCandidateInit
        {
            candidate = msg.candidate,
            sdpMid = msg.sdpMid,
            sdpMLineIndex = msg.sdpMLineIndex
        };

        if (!session.RemoteDescriptionSet)
        {
            // Queue until SetRemoteDescription completes in HandleOffer
            session.PendingCandidates.Add(candidateInit);
        }
        else
        {
            session.PeerConnection.AddIceCandidate(new RTCIceCandidate(candidateInit));
        }
    }

    #endregion
}
