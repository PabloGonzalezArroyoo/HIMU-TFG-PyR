using System;
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
