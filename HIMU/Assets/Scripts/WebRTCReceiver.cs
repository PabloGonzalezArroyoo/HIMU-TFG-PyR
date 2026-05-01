using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts;

public class WebRTCReceiver : MonoBehaviour
{
    RTCPeerConnection peer;
    [HideInInspector] public RawImage displayTarget; // UI donde se muestra el vídeo

    public System.Action<SignalingMessage> OnSignalingMessage;
    public string RemoteIp;

    public void Initialize()
    {
        Debug.Log("[WebRTC] Inicializando");

        var config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };

        peer = new RTCPeerConnection(ref config);

        peer.OnIceCandidate = candidate =>
        {
            var msg = new SignalingMessage
            {
                sourceIp = ConnectionManager.ipAddress,
                destinationIp = RemoteIp,
                type = ConnectionEvent.ICE,
                body = JsonUtility.ToJson(new IceCandidateData(candidate))
            };
            OnSignalingMessage?.Invoke(msg);
        };

        peer.OnIceConnectionChange = state =>
            Debug.Log($"[WebRTCReceiver] ICE -> {state}");

        // Aquí llega el track de vídeo cuando la conexión P2P se establece
        peer.OnTrack = e =>
        {
            Debug.Log("[WebRTC] Recibiendo video");
            if (e.Track is VideoStreamTrack videoTrack)
            {
                videoTrack.OnVideoReceived += tex =>
                {
                    if (displayTarget != null)
                    {
                        Debug.Log("[WebRTC] Aplicando stream");
                        displayTarget.texture = tex;
                    }      
                };
            }
        };
    }

    // Llamado cuando llega la SDP Offer del servidor por TCP
    public IEnumerator HandleOffer(RTCSessionDescription offer)
    {
        var setRemote = peer.SetRemoteDescription(ref offer);
        yield return setRemote;
        if (setRemote.IsError) { Debug.LogError(setRemote.Error.message); yield break; }

        var answerOp = peer.CreateAnswer();
        yield return answerOp;

        var answer = answerOp.Desc;
        var setLocal = peer.SetLocalDescription(ref answer);
        yield return setLocal;

        // Enviamos la answer de vuelta al servidor por TCP
        var msg = new SignalingMessage
        {
            sourceIp = ConnectionManager.ipAddress,
            destinationIp = RemoteIp,
            type = ConnectionEvent.SDP,
            body = JsonUtility.ToJson(new SessionDescriptionData(answer))
        };
        OnSignalingMessage?.Invoke(msg);
    }

    public void AddIceCandidate(RTCIceCandidateInit init)
    {
        peer.AddIceCandidate(new RTCIceCandidate(init));
    }

    void OnDestroy()
    {
        peer?.Close();
        peer?.Dispose();
    }
}