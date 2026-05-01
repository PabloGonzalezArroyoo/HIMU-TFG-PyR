using System;
using Unity.WebRTC;
using UnityEngine;

namespace Assets.Scripts
{
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

    [Serializable]
    public class SessionDescriptionData
    {
        public string type;  // "offer" o "answer"
        public string sdp;

        public SessionDescriptionData(RTCSessionDescription desc)
        {
            type = desc.type.ToString().ToLower();  // RTCSdpType.Offer -> "offer"
            sdp = desc.sdp;
        }

        public RTCSessionDescription ToDesc()
        {
            return new RTCSessionDescription
            {
                type = type == "offer" ? RTCSdpType.Offer : RTCSdpType.Answer,
                sdp = this.sdp
            };
        }
    }

    [Serializable]
    public class SignalingMessage
    {
        public string sourceIp;
        public string destinationIp;       // IP destino, vacío = broadcast
        public ConnectionEvent type;
        public string body;    // SDP serializado o JSON del ICE candidate
    }

    [Serializable]
    public class ConnectionData
    {
        public string ipAddress;
        public int port;
        public ConnectionEvent type;

        public ConnectionData(string ipAddress, int port, ConnectionEvent connEvent)
        {
            this.ipAddress = ipAddress;
            this.port = port;
            this.type = connEvent;
        }
    }
}
