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
        DISCONNECT
    }

    [Serializable]
    public class ConnectionData
    {
        public string ipAddress;
        public int port;
        public ConnectionEvent connEvent;

        public ConnectionData(string ipAddress, int port, ConnectionEvent connEvent)
        {
            this.ipAddress = ipAddress;
            this.port = port;
            this.connEvent = connEvent;
        }
    }
}
