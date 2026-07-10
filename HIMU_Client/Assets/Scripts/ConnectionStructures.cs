using System;
using Unity.WebRTC;
using UnityEngine;

public enum ConnectionEvent
{
    DEFAULT,
    BROADCAST,
    HANDSHAKE,
    SEND,
    DISCONNECT,
    SDP,
    ICE
}

public enum ClientType
{
    NONE,
    STREAM,
    PLAYER
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
            type = this.type == "offer" ? RTCSdpType.Offer : RTCSdpType.Answer,
            sdp = this.sdp
        };
    }
}

[Serializable]
public class SignalingMessage
{
    public string destinationIp;    // Destination IP, empty = broadcast
    public ConnectionEvent type;
    public string body;             // Serialized SDP or ICE's candidate JSON

    public SignalingMessage(string dIP, ConnectionEvent e, string b)
    {
        destinationIp = dIP;
        type = e;
        body = b;
    }
}

[Serializable]
public class ConnectionData
{
    public int port;
    public string name;
    public string info;
    public string ipAddress;
    public ConnectionEvent connType;
    public ClientType clientType;

    public ConnectionData(string ipAddress, int port, ConnectionEvent connEvent, ClientType clientType = ClientType.NONE)
    {
        this.ipAddress = ipAddress;
        this.port = port;
        this.connType = connEvent;
        this.clientType = clientType;
    }
}

[Serializable]
public class InputData
{
    public Vector2 move;        // Movement dir
    public Vector2 rotation;    // Rot delta
    public bool sprint;
    public bool moveUp;
    public bool moveDown;

    public InputData(Vector2 move, Vector2 rotation, bool sprint, bool moveUp, bool moveDown)
    {
        this.move = move;
        this.rotation = rotation;
        this.sprint = sprint;
        this.moveUp = moveUp;
        this.moveDown = moveDown;
    }
}
