using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using NativeWebSocket;

/// <summary>
/// Class incharged of seting up WebRTC and the video streaming object. It sets up WebRTC by sending
/// and processing messages related to the network protocol and making it a part of Unity's update
/// thread.
/// </summary>
public class WebRTCStreamer : MonoBehaviour
{
    #region Variables
    /// <summary>
    /// Connection between peers (local and remote)
    /// </summary>
    RTCPeerConnection peerConnection;

    /// <summary>
    /// Video data that is sent to the remote peer. WebRTC will be incharged of encoding
    /// the video that it stores in this variable so that it can be processed correctly.
    /// </summary>
    VideoStreamTrack videoTrack;

    /// <summary>
    /// Socket connected to the signaling server where all packages are sent
    /// </summary>
    WebSocket signalingSocket;

    /// <summary>
    /// The URL from which Unity will send information
    /// </summary>
    [SerializeField] string signalingUrl = "ws://localhost:8080?type=unity";

    /// <summary>
    /// Struct that represents a signaling message used between Unity, the signaling server
    /// and a client.
    /// </summary>
    [System.Serializable]
    struct SignalingMessage
    {
        /// <summary>
        /// Message type (e.g. "offer", "answer", "candidate").
        /// Determines how the message should be handled in the signaling flow.
        /// </summary>
        public string type;

        /// <summary>
        /// Session Description Protocol (SDP) string.
        /// Contains media configuration details used in offer/answer exchange.
        /// </summary>
        public string sdp;

        /// <summary>
        /// ICE candidate string.
        /// Represents a possible network path for establishing the peer-to-peer connection.
        /// </summary>
        public string candidate;

        /// <summary>
        /// Media stream identification tag for the ICE candidate.
        /// Used to associate the candidate with a specific media section.
        /// </summary>
        public string sdpMid;

        /// <summary>
        /// Index of the media description in the SDP.
        /// Helps identify which media line the ICE candidate belongs to.
        /// </summary>
        public int sdpMLineIndex;
    }

    #endregion

    #region Methods
    /// <summary>
    /// Creates an GameObject and attaches this component to ensure it is initialized
    /// before any scene is loaded. The object is marked as DontDestroyOnLoad so it
    /// persists across scene transitions.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateInstance()
    {
        var obj = new GameObject("WebRTCStreamer");
        obj.AddComponent<WebRTCStreamer>();
        Object.DontDestroyOnLoad(obj);
    }


    /// <summary>
    /// Initializes and configures the WebRTC connection.
    /// Waits until a captured frame is available, creates a video track from it,
    /// sets up the peer connection with STUN servers, registers event handlers,
    /// and starts the signaling process to establish the connection.
    /// </summary>
    /// <returns>IEnumerator required by Unity's coroutine system</returns>
    IEnumerator SetupWebRTC()
    {
        // Don't initialize WebRTC until a frame is captured
        yield return new WaitUntil(() => FrameCaptureFeature.Instance?.GetFrame() != null);

        RenderTexture rt = FrameCaptureFeature.Instance.GetFrame();
        Debug.Log($"RenderTexture: {rt.width}x{rt.height}, format: {rt.format}, created: {rt.IsCreated()}");

        videoTrack = new VideoStreamTrack(rt);

        // Creates the WebRTC connection using an specified configuration.
        var config = new RTCConfiguration
        {
            // Servers used to discover web routes. Google STUN obtains the public IP corresponding
            // to this deviece to surpass the NAT adress translation in the private network
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };

        // Core of the connection, incharge of handling ICE and tracks
        peerConnection = new RTCPeerConnection(ref config);

        // Callback for when a possible route is discovered (candidate).
        // It creates a signaling message to send to the other peer through the server
        // to stablish connection with the ICE protocol.
        peerConnection.OnIceCandidate = candidate =>
        {
            SendSignalingMessage(new SignalingMessage
            {
                type = "ice",
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex ?? 0
            });
        };

        // Debug callback to show which state was changed the connection to
        peerConnection.OnConnectionStateChange = state =>
        {
            Debug.Log($"WebRTC state: {state}");
            if (state == RTCPeerConnectionState.Connected)
            {
                Debug.Log($"VideoTrack enabled: {videoTrack.Enabled}, readyState: {videoTrack.ReadyState}");
            }
        };

        // Add the video tracker to the established connection so that it can be sent. This is
        // who sends the video for streaming, is souley handled by WebRTC and not us.
        peerConnection.AddTrack(videoTrack);

        // Wait till the connection is done
        yield return StartCoroutine(ConnectSignaling());
    }


    /// <summary>
    /// Establishes the connection with the Webscoket signaling server, overriding callbacks for
    /// messages, connections and errors; and manages the main loop for receiving and processing
    /// messages.
    /// </summary>
    /// <returns>
    /// IEnumerator requiered by Unity's coroutine system
    /// </returns>
    IEnumerator ConnectSignaling()
    {
        // Creates a websocket that points to the signaling server
        signalingSocket = new WebSocket(signalingUrl);

        // Queue to store incoming messages in a safe-thread manner since WebScokect 
        // callbacks are managed on another thread
        var messageQueue = new System.Collections.Generic.Queue<string>();

        // Callback for when a binary message arrives. It enqueues in the prior message queue.
        signalingSocket.OnMessage += bytes =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            messageQueue.Enqueue(json);
        };

        // Control flags
        bool connected = false;
        bool error = false;

        // Changes flags to mark the connection
        signalingSocket.OnOpen += () => connected = true;

        // Changes the flag to mark an error ocurred
        signalingSocket.OnError += e =>
        {
            Debug.LogError("WebSocket error: " + e);
            error = true;
        };

        // Starts the connection asynchronously (doesn't block the thread)
        // NOTE: "_" means "discard" to the compiler in C# so that no warning is shown in the
        // console for not saving the vaule returned by Connect()
        _ = signalingSocket.Connect();

        // Suspends the corutine until it has connected succesfuly or if an error ocurred
        yield return new WaitUntil(() => connected || error);

        // Logs the error and exits the coroutine early if it ocurred
        if (error)
        {
            Debug.LogError("No se pudo conectar al servidor de señalización");
            yield break;
        }

        Debug.Log("Conectado al servidor de señalización");

        // Main loop: dispatches WebSocket messages and processes the queue
        while (true)
        {
            // Moves messages from the network thread to Unity's main thread, which triggers
            // OnMessage callbacks
            signalingSocket.DispatchMessageQueue();

            // Processes all messages that arrived during this frame
            while (messageQueue.Count > 0)
            {
                string json = messageQueue.Dequeue();

                // Each message is processed in a new corutine, and suspends this one
                // until the message has been processed
                yield return StartCoroutine(HandleSignalingMessage(json));
            }

            // Gives control back to Unity until the next frame avoiding thread blocking
            // NOTE: This is Unity's corutine standar for pausing one while saving all the
            // data it generated. Is a way of the corutine to say "my work for this frame
            // is done, keep going till is my turn in the next frame". It doesn't exit the
            // while loop because it doesn't work as a "return" or "break".
            yield return null;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="json">JSON message to be handled</param>
    /// <returns>IEnumerator required by Unity's coroutine system</returns>
    IEnumerator HandleSignalingMessage(string json)
    {
        // Formats the string recieved into a JSON
        var msg = JsonUtility.FromJson<SignalingMessage>(json);

        // NOTE: Offer messages indicates what we are going to send and how. An "SDP offer" is sent,
        // which contains the peer's capabilities (codes, resolution, data type...). It can be seen
        // as a format agreement.
        if (msg.type == "offer")
        {
            Debug.Log("Oferta recibida, generando respuesta...");

            // Builds a RTCSessionDescription from the recieved SDP offer.
            var desc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = msg.sdp };

            // Applies the remote SDP description to the peer connection so that both peers can
            // exchange messages they support and waits for completion
            var setRemote = peerConnection.SetRemoteDescription(ref desc);
            yield return setRemote;
            if (setRemote.IsError)
            {
                Debug.LogError("Error en SetRemoteDescription: " + setRemote.Error.message);
                yield break;
            }

            // Generates an SDP answer to be sent to the remote peer so that it can also configure
            // its connection with the capabilities of this peer and waits for completion
            var answerOp = peerConnection.CreateAnswer();
            yield return answerOp;
            if (answerOp.IsError)
            {
                Debug.LogError("Error en CreateAnswer: " + answerOp.Error.message);
                yield break;
            }

            // Retrieves the generated answer and applies it as the local SDP description
            var answer = answerOp.Desc;
            var setLocal = peerConnection.SetLocalDescription(ref answer);
            yield return setLocal;

            // Sends the SDP answer back to the remote peer through the signaling server
            SendSignalingMessage(new SignalingMessage { type = "answer", sdp = answer.sdp });
            Debug.Log("Respuesta enviada");
        }

        // NOTE: ICE messages indicates where are we going to send it. An "ICE candidate" is sent,
        // which contains the network route (local IP, public IP...) so that devieces can communicate
        // with each other even if NATs or Firewalls are on their way.
        else if (msg.type == "ice")
        {
            // Builds an RTCIceCandidateInit from the received ICE candidate data
            var candidate = new RTCIceCandidateInit
            {
                candidate = msg.candidate,
                sdpMid = msg.sdpMid,
                sdpMLineIndex = msg.sdpMLineIndex
            };

            // Resgisters the ICE candidate into the peer connection so that they can negotitate
            // the best available network route
            peerConnection.AddIceCandidate(new RTCIceCandidate(candidate));
        }
    }


    /// <summary>
    /// Converts the message to be sent in JSON format and sends it in UTF8 format
    /// through the connection's socket
    /// </summary>
    /// <param name="msg">Information to be sent</param>
    async void SendSignalingMessage(SignalingMessage msg)
    {
        if (signalingSocket?.State == WebSocketState.Open)
        {
            string json = JsonUtility.ToJson(msg);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await signalingSocket.Send(bytes);
        }
    }
    #endregion

    #region Monobehaviour
    /// <summary>
    /// Initializes WebRTC and its update rutine
    /// </summary>
    void Start()
    {
        StartCoroutine(WebRTC.Update());
        StartCoroutine(SetupWebRTC());
    }

    /// <summary>
    /// Dispatches enqued messages to the signaling server
    /// </summary>
    void Update()
    {
        signalingSocket?.DispatchMessageQueue();
    }

    /// <summary>
    /// Safely closes the connection components once this object is destroyed
    /// </summary>
    void OnDestroy()
    {
        videoTrack?.Dispose();
        peerConnection?.Close();
        peerConnection?.Dispose();
        signalingSocket?.Close();
    }
    #endregion
}