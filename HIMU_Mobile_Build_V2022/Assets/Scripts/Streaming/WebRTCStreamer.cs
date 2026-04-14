using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using NativeWebSocket;

public class WebRTCStreamer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateInstance()
    {
        var obj = new GameObject("WebRTCStreamer");
        obj.AddComponent<WebRTCStreamer>();
        Object.DontDestroyOnLoad(obj);
    }

    RTCPeerConnection peerConnection;
    VideoStreamTrack videoTrack;
    WebSocket signalingSocket;

    [SerializeField] string signalingUrl = "ws://localhost:8080?type=unity";

    void Start()
    {
        StartCoroutine(WebRTC.Update());
        StartCoroutine(SetupWebRTC());
    }

    IEnumerator SetupWebRTC()
    {
        yield return new WaitUntil(() => FrameCaptureFeature.Instance?.CapturedFrame != null);

        RenderTexture rt = FrameCaptureFeature.Instance.CapturedFrame;

        Debug.Log($"RenderTexture: {rt.width}x{rt.height}, format: {rt.format}, created: {rt.IsCreated()}");

        videoTrack = new VideoStreamTrack(rt);

        var config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };

        peerConnection = new RTCPeerConnection(ref config);

        peerConnection.OnIceCandidate = candidate =>
        {
            // Esto es fire-and-forget, no necesita await
            SendSignalingMessage(new SignalingMessage
            {
                type = "ice",
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex ?? 0
            });
        };

        peerConnection.OnConnectionStateChange = state =>
        {
            Debug.Log($"WebRTC state: {state}");
            if (state == RTCPeerConnectionState.Connected)
            {
                Debug.Log($"VideoTrack enabled: {videoTrack.Enabled}, readyState: {videoTrack.ReadyState}");
            }
        };

        peerConnection.AddTrack(videoTrack);

        yield return StartCoroutine(ConnectSignaling());
    }

    IEnumerator ConnectSignaling()
    {
        signalingSocket = new WebSocket(signalingUrl);

        // Guardamos los mensajes entrantes en una cola para procesarlos
        // en la corrutina principal, evitando yield dentro de lambdas
        var messageQueue = new System.Collections.Generic.Queue<string>();

        signalingSocket.OnMessage += bytes =>
        {
            // El lambda solo encola, no hace nada async
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            messageQueue.Enqueue(json);
        };

        signalingSocket.OnError += e => Debug.LogError("WebSocket error: " + e);

        // Lanzamos la conexión como Task y esperamos con yield
        bool connected = false;
        bool error = false;

        signalingSocket.OnOpen += () => connected = true;
        signalingSocket.OnError += _ => error = true;

        _ = signalingSocket.Connect(); // no awaiteamos aquí, esperamos con yield

        yield return new WaitUntil(() => connected || error);

        if (error)
        {
            Debug.LogError("No se pudo conectar al servidor de señalización");
            yield break;
        }

        Debug.Log("Conectado al servidor de señalización");

        // Bucle principal: despacha mensajes WebSocket y procesa la cola
        while (true)
        {
            signalingSocket.DispatchMessageQueue();

            while (messageQueue.Count > 0)
            {
                string json = messageQueue.Dequeue();
                yield return StartCoroutine(HandleSignalingMessage(json));
            }

            yield return null;
        }
    }

    IEnumerator HandleSignalingMessage(string json)
    {
        var msg = JsonUtility.FromJson<SignalingMessage>(json);

        if (msg.type == "offer")
        {
            Debug.Log("Oferta recibida, generando respuesta...");

            var desc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = msg.sdp };
            var setRemote = peerConnection.SetRemoteDescription(ref desc);
            yield return setRemote;

            if (setRemote.IsError)
            {
                Debug.LogError("Error en SetRemoteDescription: " + setRemote.Error.message);
                yield break;
            }

            var answerOp = peerConnection.CreateAnswer();
            yield return answerOp;

            if (answerOp.IsError)
            {
                Debug.LogError("Error en CreateAnswer: " + answerOp.Error.message);
                yield break;
            }

            var answer = answerOp.Desc;
            var setLocal = peerConnection.SetLocalDescription(ref answer);
            yield return setLocal;

            SendSignalingMessage(new SignalingMessage { type = "answer", sdp = answer.sdp });
            Debug.Log("Respuesta enviada");
        }
        else if (msg.type == "ice")
        {
            var candidate = new RTCIceCandidateInit
            {
                candidate = msg.candidate,
                sdpMid = msg.sdpMid,
                sdpMLineIndex = msg.sdpMLineIndex
            };
            peerConnection.AddIceCandidate(new RTCIceCandidate(candidate));
        }
    }

    async void SendSignalingMessage(SignalingMessage msg)
    {
        if (signalingSocket?.State == WebSocketState.Open)
        {
            string json = JsonUtility.ToJson(msg);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await signalingSocket.Send(bytes); // envío explícito como bytes UTF8
        }
    }

    void Update()
    {
        signalingSocket?.DispatchMessageQueue();
    }

    void OnDestroy()
    {
        videoTrack?.Dispose();
        peerConnection?.Close();
        peerConnection?.Dispose();
        signalingSocket?.Close();
    }

    [System.Serializable]
    class SignalingMessage
    {
        public string type;
        public string sdp;
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }
}