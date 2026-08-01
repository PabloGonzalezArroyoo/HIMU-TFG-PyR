using System;
using System.Net.Sockets;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;

public class ClientSignalingHandler : MonoBehaviour
{
    #region Variables

    /// <summary>
    /// Instance of ClientSignalingHandler (Singleton)
    /// </summary>
    public static ClientSignalingHandler Instance { get; private set; }

    /// <summary>
    /// Host's TCP connection
    /// </summary>
    private TcpClient hostConnection;

    /// <summary>
    /// TCP stream from where the communication (mainly on handshake, ICE and SDP offers) will happen.
    /// </summary>
    private NetworkStream stream;

    /// <summary>
    /// Thread where the communication will happen.
    /// </summary>
    private Thread readThread;

    /// <summary>
    /// Wether the server is running or not.
    /// </summary>
    private bool running;

    /// <summary>
    /// Component that allows the WebRTC communication.
    /// </summary>
    private WebRTCReceiver receiver;

    /// <summary>
    /// Object in the scene where the video received will be shown.
    /// </summary>
    [SerializeField]
    private RawImage videoPanel;

    #endregion

    #region Connection

    /// <summary>
    /// Saves all the connection data, creates and configures the WebRTC receiver and launches
    /// the read loop.
    /// </summary>
    /// <param name="hostTcp">Object used by the client to talk with the host</param>
    /// <param name="tcpStream">TCP Stream to read/write from.</param>
    public void StartSession(TcpClient hostTcp, NetworkStream tcpStream)
    {
        hostConnection = hostTcp;
        stream = tcpStream;
        running = true;

        // Create and configure the receiver
        var go = new GameObject("WebRTCReceiver");
        receiver = go.AddComponent<WebRTCReceiver>();
        receiver.SetUpAndInitialize(videoPanel, SendMessage);

        // NOTE: maybe this has to be changed or documented, because it assumes that the same go
        // has at least both ClienSignalingHandler and InputManager attached to it.
        GetComponent<ClientInputManager>().SetReceiver(receiver);

        readThread = new Thread(ReadLoop) { IsBackground = true };
        readThread.Start();
    }

    /// <summary>
    /// Loop incharged of reading the streamed network data.
    /// </summary>
    private void ReadLoop()
    {
        while (running)
        {
            try
            {
                if (!NetworkUtils.TryReadFramedMessage(stream, out string json)) break;

                var msg = JsonUtility.FromJson<SignalingMessage>(json);

                // Despachar al main thread
                UnityMainThreadDispatcher.Instance()?.Enqueue(() => HandleMessage(msg));
            }
            catch (Exception e)
            {
                if (running) Debug.LogWarning($"[ClientSignaling] {e.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// For when a SignalingMessage is received. Handles SDP Offers and ICE candidates delegating the
    /// information to the WebRTCReceiver.
    /// </summary>
    /// <param name="msg">Signaling message.</param>
    private void HandleMessage(SignalingMessage msg)
    {
        if (msg.type == ConnectionEvent.SDP)
        {
            SessionDescriptionData data = JsonUtility.FromJson<SessionDescriptionData>(msg.body);
            RTCSessionDescription offer = data.ToRTCDesc();
            StartCoroutine(receiver.HandleOffer(offer));
        }
        else if (msg.type == ConnectionEvent.ICE)
        {
            IceCandidateData data = JsonUtility.FromJson<IceCandidateData>(msg.body);
            RTCIceCandidateInit init = new RTCIceCandidateInit
            {
                candidate = data.candidate,
                sdpMid = data.sdpMid,
                sdpMLineIndex = data.sdpMLineIndex
            };
            receiver.AddIceCandidate(init);
        }
    }

    /// <summary>
    /// Sends a SignalingMessage throught the TCP stream.
    /// </summary>
    /// <param name="msg">Signaling message.</param>
    private void SendMessage(SignalingMessage msg)
    {
        NetworkUtils.WriteFramedMessage(stream, JsonUtility.ToJson(msg));
    }

    #endregion

    #region Monobehaviour

    void Awake()
    {
        if (Instance) { DestroyImmediate(gameObject); return; }
        Instance = this;
        StartCoroutine(WebRTC.Update());
    }

    private void Start()
    {
        running = false;
    }

    void OnDestroy()
    {
        running = false;
        stream?.Close();
        hostConnection?.Close();
        readThread?.Join(500);
    }

    #endregion
}