using System;
using System.Net.Sockets;
using Unity.WebRTC;
using UnityEngine;

/// <summary>
/// HOST CONNECTION STRUCTURES
/// </summary>

public enum ConnectionEvent
{
    DEFAULT,
    BROADCAST,
    HANDSHAKE,
    SEND,
    DISCONNECT,
    SDP,        // SDP: Session Description Protocol (offer/answer)
    ICE         // ICE: Interactive Connectivity Establishment (ICE candidates)
}

public enum ConnectionTransport
{
    NONE,
    TCP,
    WebSocket,
    ADB
}

public enum ClientType
{
    NONE,
    WEB_SOCKET,
    TCP,
    ADB
}

[Serializable]
public class SignalingMessage
{
    public ConnectionEvent type;
    public string body;             // Serialized SDP or ICE's candidate JSON

    public SignalingMessage(ConnectionEvent e, string b)
    {
        type = e;
        body = b;
    }
}

[Serializable]
public class IceCandidateData
{
    public string candidate;
    public string sdpMid;
    public int sdpMLineIndex;

    public IceCandidateData(RTCIceCandidate c)
    {
        candidate = c.Candidate;
        sdpMid = c.SdpMid;
        sdpMLineIndex = c.SdpMLineIndex ?? 0;
    }
}

// Wrapper that saves the values of RTCSessionDescription
[Serializable]
public class SessionDescriptionData
{
    public string type;  // "offer" or "answer"
    public string sdp;

    public SessionDescriptionData(RTCSessionDescription desc)
    {
        type = desc.type.ToString().ToLower();  // RTCSdpType.Offer -> "offer"
        sdp = desc.sdp;
    }

    public RTCSessionDescription ToRTCDesc()
    {
        return new RTCSessionDescription
        {
            type = type == "offer" ? RTCSdpType.Offer : RTCSdpType.Answer,
            sdp = this.sdp
        };
    }
}

[Serializable]
public class ConnectionData
{
    public string ipAddress;
    public int port;
    public ConnectionEvent connType;
    public ClientType clientType;

    private ConnectionData(string ipAddress, int port, ConnectionEvent connEvent, ClientType clientType = ClientType.NONE)
    {
        this.ipAddress = ipAddress;
        this.port = port;
        this.connType = connEvent;
        this.clientType = clientType;
    }

    /// <summary>
    /// Payload that the host broadcasts over UDP multicast so nearby devices can discover it.
    /// </summary>
    /// <param name="hostIP">Host's IP, so the client knows where to connect.</param>
    /// <param name="listenPort">TCP port SignalingServer is listening on.</param>
    public static ConnectionData ForBroadcast(string hostIP, int listenPort)
    {
        return new ConnectionData(hostIP, listenPort, ConnectionEvent.BROADCAST, ClientType.NONE);
    }

    /// <summary>
    /// Payload a client sends back over TCP to complete the handshake and register itself.
    /// </summary>
    /// <param name="clientIP">Client's own IP, used as its identifier until replaced by a GUID.</param>
    /// <param name="clientType">Declares what kind of client this device is.</param>
    public static ConnectionData ForHandshake(string clientIP, ClientType clientType)
    {
        return new ConnectionData(clientIP, 0, ConnectionEvent.HANDSHAKE, clientType);
    }
}

// Input structures
[Serializable]
public class TouchesData
{
    public int id;
    public Vector2 pos;

    public TouchesData(int _id, Vector2 _pos)
    {
        id = _id;
        pos = _pos;
    }
}

[Serializable]
public class InputFrame
{
    public TouchesData[] touches;
    public Vector3 accelometer;
    // GPS?
    // Micro

    public InputFrame(TouchesData[] t, Vector3 a)
    {
        touches = t;
        accelometer = a;
    }
}

// WebSocket communication structures
[Serializable] public class WSBaseMessage { public int type; }

[Serializable] public class WSNewClientMessage { public int type; public int clientId; }

[Serializable]
public class WSTaggedMessage
{
    public int type;
    public int clientId;
    public string body;
}