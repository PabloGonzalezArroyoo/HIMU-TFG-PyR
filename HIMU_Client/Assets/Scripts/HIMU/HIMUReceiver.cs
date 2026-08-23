using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// This component represents the client's connection with the host via WebRTC.
/// </summary>
public class HIMUReceiver : MonoBehaviour
{
    #region Variables
    /// <summary>
    /// Object that represents the P2P connection.
    /// </summary>
    RTCPeerConnection peer;

    /// <summary>
    /// RTC Channel where data will be sent or received from the peer.
    /// </summary>
    RTCDataChannel dataChannel;

    /// <summary>
    /// UI where the streamed image is displayed.
    /// </summary>
    [SerializeField]
    private RawImage displayTarget;

    /// <summary>
    /// Callback for when a signaling message is received.
    /// </summary>
    public System.Action<SignalingMessage> OnSignalingMessage;
    #endregion

    #region Methods
    /// <summary>
    /// Initializes the WebRTC connection and overrides callbacks for when data is sent through it.
    /// </summary>
    private void Initialize()
    {
        Debug.Log("[WebRTCReceiver] Initializing client.");

        RTCConfiguration config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };

        peer = new RTCPeerConnection(ref config);

        peer.OnIceCandidate = candidate =>
        {
            SignalingMessage msg = new SignalingMessage(ConnectionEvent.ICE, JsonUtility.ToJson(new IceCandidateData(candidate)));
            OnSignalingMessage?.Invoke(msg);
        };

        peer.OnIceConnectionChange = state => Debug.Log($"[WebRTCReceiver] ICE -> {state}");

        // Aquí llega el track de vídeo cuando la conexión P2P se establece
        peer.OnTrack = e =>
        {
            if (e.Track is VideoStreamTrack videoTrack)
                videoTrack.OnVideoReceived += tex =>
                    UnityMainThreadDispatcher.Instance().Enqueue(() => ProcessVideo(tex));
        };

        // Configuración del callback de datos
        peer.OnDataChannel = channel =>
        {
            dataChannel = channel;
            Debug.Log($"[WebRTCReceiver] DataChannel received: {channel.Label}");

            channel.OnOpen = () => Debug.Log("[DataChannel] Opened in client.");
            channel.OnClose = () => Debug.Log("[DataChannel] Closed in client.");
            channel.OnMessage = bytes =>
                UnityMainThreadDispatcher.Instance().Enqueue(() => ProcessData(bytes));
        };
    }

    public void SetUpAndInitialize(System.Action<SignalingMessage> action)
    {
        OnSignalingMessage = action;
        Initialize();
    }

    /// <summary>
    /// Decodes the SDP offer from the host and sends an SDP offer back with what has been received.
    /// </summary>
    /// <param name="offer">SDP offer.</param>
    /// <returns></returns>
    public IEnumerator HandleOffer(RTCSessionDescription offer)
    {
        RTCSetSessionDescriptionAsyncOperation setRemote = peer.SetRemoteDescription(ref offer);
        yield return setRemote;
        if (setRemote.IsError) { Debug.LogError(setRemote.Error.message); yield break; }

        RTCSessionDescriptionAsyncOperation answerOp = peer.CreateAnswer();
        yield return answerOp;

        RTCSessionDescription answer = answerOp.Desc;
        RTCSetSessionDescriptionAsyncOperation setLocal = peer.SetLocalDescription(ref answer);
        yield return setLocal;

        // Enviamos la answer de vuelta al servidor por TCP
        SignalingMessage msg = 
            new SignalingMessage(ConnectionEvent.SDP, JsonUtility.ToJson(new SessionDescriptionData(answer)));
        OnSignalingMessage?.Invoke(msg);
    }

    /// <summary>
    /// Adds the ICE candidate to this peer.
    /// </summary>
    /// <param name="init">Initial ICE candidate.</param>
    public void AddIceCandidate(RTCIceCandidateInit init)
    {
        peer.AddIceCandidate(new RTCIceCandidate(init));
    }

    /// <summary>
    /// Processes data received form a DataChannel.
    /// </summary>
    /// <param name="b">Block of data.</param>
    private void ProcessData(byte[] b)
    {
        string msg = System.Text.Encoding.UTF8.GetString(b);
        Debug.Log($"[DataChannel] Mensaje recibido: {msg}");
    }

    /// <summary>
    /// Processes the video texture that has been received from the VideoTrack object.
    /// </summary>
    /// <param name="tex">Texture received.</param>
    private void ProcessVideo(Texture tex)
    {
        Debug.Log("[WebRTCReceiver] Receiving video...");
        if (displayTarget != null)
        {
            Debug.Log("[WebRTCReceiver] Applying stream.");
            displayTarget.texture = tex;
        }
    }

    /// <summary>
    /// Sends a JSON through the opened DataChannel
    /// </summary>
    /// <param name="json"></param>
    public void SendThroughDataChannel(string json)
    {
        if (dataChannel != null && dataChannel.ReadyState == RTCDataChannelState.Open)
        {
            Debug.Log(json);
            dataChannel.Send(json);
        }
    }
    #endregion

    #region MonoBehaviour
    void OnDestroy()
    {
        dataChannel?.Close();
        dataChannel?.Dispose();
        peer?.Close();
        peer?.Dispose();
    }
    #endregion
}