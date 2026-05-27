using Assets.Scripts;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente asociado al objeto creado que representa a un cliente
/// </summary>
public class WebRTCReceiver : MonoBehaviour
{
    #region Variables
    /// <summary>
    /// Object that represents the P2P connection
    /// </summary>
    RTCPeerConnection peer;

    /// <summary>
    /// RTC Channel where data will be sent or recieved from the peer
    /// </summary>
    RTCDataChannel dataChannel;

    /// <summary>
    /// UI where the streamed image is displayed
    /// </summary>
    [HideInInspector] public RawImage displayTarget;

    /// <summary>
    /// Callback for when a signaling message is recieved
    /// </summary>
    public System.Action<SignalingMessage> OnSignalingMessage;

    /// <summary>
    /// Host IP
    /// </summary>
    public string RemoteIp;
    #endregion

    #region Methods
    public void Initialize()
    {
        Debug.Log("[WebRTC] Inicializando cliente");

        RTCConfiguration config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };

        peer = new RTCPeerConnection(ref config);

        peer.OnIceCandidate = candidate =>
        {
            SignalingMessage msg = new SignalingMessage(ConnectionManager.ipAddress, RemoteIp, ConnectionEvent.ICE, JsonUtility.ToJson(new IceCandidateData(candidate)));
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
            Debug.Log($"[WebRTCReceiver] DataChannel recibido: {channel.Label}");

            channel.OnOpen = () => Debug.Log("[DataChannel] Abierto en cliente");
            channel.OnClose = () => Debug.Log("[DataChannel] Cerrado en cliente");
            channel.OnMessage = bytes =>
                UnityMainThreadDispatcher.Instance().Enqueue(() => ProcessData(bytes));
        };
    }

    // Llamado cuando llega la SDP Offer del servidor por TCP
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
            new SignalingMessage(ConnectionManager.ipAddress, RemoteIp, ConnectionEvent.SDP, JsonUtility.ToJson(new SessionDescriptionData(answer)));
        OnSignalingMessage?.Invoke(msg);
    }

    public void AddIceCandidate(RTCIceCandidateInit init)
    {
        peer.AddIceCandidate(new RTCIceCandidate(init));
    }

    private void ProcessData(byte[] b)
    {
        string msg = System.Text.Encoding.UTF8.GetString(b);
        Debug.Log($"[DataChannel] Mensaje recibido: {msg}");
        // TO-DO: process data from server
    }

    private void ProcessVideo(Texture tex)
    {
        Debug.Log("[WebRTC] Recibiendo video");
        if (displayTarget != null)
        {
            Debug.Log("[WebRTC] Aplicando stream");
            displayTarget.texture = tex;
        }
    }

    public void SendInput(string json)
    {
        Debug.Log(dataChannel);
        Debug.Log(dataChannel.ReadyState);
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
        peer?.Close();
        peer?.Dispose();
    }
    #endregion
}