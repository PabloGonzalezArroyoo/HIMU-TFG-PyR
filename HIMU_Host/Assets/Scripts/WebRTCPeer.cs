using System;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

public class WebRTCPeer : MonoBehaviour
{

    #region Variables

    /// <summary>
    /// Object that represents the P2P connection.
    /// </summary>
    RTCPeerConnection peer;

    /// <summary>
    /// This client ID in the overall connected network.
    /// </summary>
    private string clientID;

    /// <summary>
    /// Referencia al sender del video track.
    /// </summary>
    RTCRtpSender videoSender;

    /// <summary>
    /// Object incharged of tracking the RenderTexture encodiing and transmision.
    /// </summary>
    VideoStreamTrack videoTrack;

    /// <summary>
    /// Texture where the camera's view will be stored.
    /// </summary>
    public RenderTexture renderTexture;

    /// <summary>
    /// Object incharged of tracking JSON packages.
    /// </summary>
    RTCDataChannel inputTrack;

    /// <summary>
    /// Callback to send SDP/ICE to the remote client via TCP.
    /// </summary>
    public System.Action<SignalingMessage> OnSignalingMessage;

    /// <summary>
    /// Array of inputs recieved in the latest message reception from the peer.
    /// Volatile so it can be accesible and updated for all threads that could access it
    /// at the same time.
    /// </summary>
    private volatile TouchesData[] latestTouches = Array.Empty<TouchesData>();

    /// <summary>
    /// Intermediate object between the input registered in this component and the other
    /// scripts that could use it. It uses the ReadOnly interface so that there is no way for
    /// external scripts to overwrite or add any data to the input cached.
    /// </summary>
    public IReadOnlyList<TouchesData> CurrentTouches
    {
        get { return latestTouches; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Initializes all WebRTC components for this peer: WebRTC configuration and streaming and data
    /// reception callbacks.
    /// </summary>
    /// <param name="id">Id of this client.</param>
    /// <param name="rt">RenderTexture of the attached camera for streaming.</param>
    /// <param name="onSignalingMsg">Callback for when a SignalingMessage is received.</param>
    public void Initialize(string id, RenderTexture rt, System.Action<SignalingMessage> onSignalingMsg)
    {
        clientID = id;
        renderTexture = rt;
        OnSignalingMessage = onSignalingMsg;

        // Connection configuration. Uses STUN to discover the public IP of this device.
        RTCConfiguration config = new RTCConfiguration
        {
            iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
        };

        // Creates the connection with the previous configuration.
        peer = new RTCPeerConnection(ref config);

        // Callback for when receiving a candidate.
        peer.OnIceCandidate = candidate =>
        {
            SignalingMessage msg = new SignalingMessage(ConnectionEvent.ICE, JsonUtility.ToJson(new IceCandidateData(candidate)));
            OnSignalingMessage?.Invoke(msg);
        };

        peer.OnIceConnectionChange = state =>
            Debug.Log($"[WebRTCPeer] ICE state -> {state}");

        // Add videotrack to the connection.
        videoTrack = new VideoStreamTrack(renderTexture);
        videoSender = peer.AddTrack(videoTrack);

        // Data channel configuration.
        var dataChannelConfig = new RTCDataChannelInit { ordered = false, maxRetransmits = 0 };
        inputTrack = peer.CreateDataChannel("input", dataChannelConfig);

        inputTrack.OnOpen = () => Debug.Log("[DataChannel] Open");
        inputTrack.OnClose = () => Debug.Log("[DataChannel] Closed");
        inputTrack.OnMessage = bytes =>
        {
            string msg = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log($"[DataChannel] Recieved Message: {msg}");
            InputFrame frame = JsonUtility.FromJson<InputFrame>(msg);
            latestTouches = frame.touches;
        };
    }

    /// <summary>
    /// Called when the remote client sends their SDP Answer.
    /// </summary>
    /// <param name="answer">Client's SDP Answer.</param>
    /// <returns></returns>
    public IEnumerator SetRemoteAnswer(RTCSessionDescription answer)
    {
        RTCSetSessionDescriptionAsyncOperation op = peer.SetRemoteDescription(ref answer);
        yield return op;
        if (op.IsError) Debug.LogError($"[WebRTCPeer] SetRemoteDescription: {op.Error.message}");
    }

    /// <summary>
    /// Called when an ICE candidate is received from the remote client. Adds it to the connection.
    /// </summary>
    /// <param name="init">Initialization options for creating an ICE candidate.</param>
    public void AddIceCandidate(RTCIceCandidateInit init)
    {
        peer.AddIceCandidate(new RTCIceCandidate(init));
    }

    /// <summary>
    /// Generates an SDP offer and sends it.
    /// </summary>
    /// <returns></returns>
    public IEnumerator CreateOffer()
    {
        // Create offer
        RTCSessionDescriptionAsyncOperation offerOp = peer.CreateOffer();
        yield return offerOp;

        // Assign the qualities of this device.
        RTCSessionDescription offer = offerOp.Desc;
        RTCSetSessionDescriptionAsyncOperation setOp = peer.SetLocalDescription(ref offer);
        yield return setOp;

        // Sends the SignalingMessage with this information.
        string json = JsonUtility.ToJson(new SessionDescriptionData(offer));
        SignalingMessage msg = new SignalingMessage(ConnectionEvent.SDP, json);
        OnSignalingMessage?.Invoke(msg);
    }

    /// <summary>
    /// Returns the video track object.
    /// </summary>
    /// <returns>Video track object.</returns>
    public RTCRtpSender GetVideoSender()
    {
        return videoSender;
    }
    #endregion

    #region Monobehaviour

    /// <summary>
    /// Closes the connection and cleans its objects.
    /// </summary>
    void OnDestroy()
    {
        videoTrack?.Dispose();
        peer?.Close();
        peer?.Dispose();
    }
    #endregion
}

