using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;

/// <summary>
/// HOST CONNECTION STRUCTURES
/// </summary>

#region Enums

/// <summary>
/// Indicates which fase the message we are sending belongs too
/// </summary>
public enum ConnectionEvent
{
    BROADCAST,
    HANDSHAKE,
    DISCONNECT,
    SDP, 
    ICE
}

/// <summary>
/// Indicates the type of connection stablished with a client
/// </summary>
public enum ClientConnectionType
{
    NONE,
    WEB_SOCKET,
    TCP,
    ADB
}
#endregion

/// <summary>
/// Class that represents each device connected via USB cable
/// </summary>
public class WiredDeviceData
{
    public string deviceId;
    public int localPort;
    public TcpListener listener;
    public Thread acceptThread;
    public TcpClient tcpClient;
    public string clientID;
}

/// <summary>
/// Class that encapsulates information needed when establishing connection between devices
/// </summary>
[Serializable]
public class ConnectionData
{
    public string sessionName;
    public int sessionID;
    public string ipAddress;
    public int port;
    public ConnectionEvent connType;
    public ClientConnectionType clientType;

    private ConnectionData(string ipAddress, int port, ConnectionEvent connEvent, ClientConnectionType clientType = ClientConnectionType.NONE)
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
        return new ConnectionData(hostIP, listenPort, ConnectionEvent.BROADCAST, ClientConnectionType.NONE);
    }

    /// <summary>
    /// Payload a client sends back over TCP to complete the handshake and register itself.
    /// </summary>
    /// <param name="clientIP">Client's own IP, used as its identifier until replaced by a GUID.</param>
    /// <param name="clientType">Declares what kind of client this device is.</param>
    public static ConnectionData ForHandshake(string clientIP, ClientConnectionType clientType)
    {
        return new ConnectionData(clientIP, 0, ConnectionEvent.HANDSHAKE, clientType);
    }
}

/// <summary>
/// Class that contains an unique identifier 
/// </summary>
public class ClientData
{
    public string clientID;             // En el caso de WebSocket es el clientID que nos llega del servidor de Node, en los otros es un GUID
    public ClientConnectionType type;
    public HIMUClient himuClient;

    private ClientData(ClientConnectionType type, string clientID)
    {
        this.type = type;
        this.clientID = clientID;
    }

    /// <summary>
    /// Builds a ClientData for a TCP device that just completed the handshake.
    /// </summary>
    /// <param name="connData">Handshake payload received from the device.</param>
    /// <param name="clientID">GUID assigned by SignalingServer to key this client.</param>
    public static ClientData ForDevice(ConnectionData connData, string clientID)
    {
        return new ClientData(connData.clientType, clientID);
    }

    /// <summary>
    /// Builds a ClientData for a browser client registered via the Node WebSocket.
    /// Browsers have no IP/port visible to Unity, so the relay's session id is used directly
    /// as the identifier instead of faking network fields through ConnectionData.
    /// </summary>
    /// <param name="clientID">Unique key for this client received from Node server</param>
    public static ClientData ForBrowser(string clientID)
    {
        return new ClientData(ClientConnectionType.WEB_SOCKET, clientID);
    }


    public static ClientData ForADB(string clientID)
    {
        return new ClientData(ClientConnectionType.ADB, clientID);
    }
}

#region Communication Structures
/// <summary>
/// Message shared between two devices
/// </summary>
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

/// <summary>
/// Structure that represents the entire input state from a connected device
/// </summary>
[Serializable]
public class InputFrame
{
    public List<Vector2> touches;
    public Vector3 accelometer;

    public InputFrame(List<Vector2> t, Vector3 a)
    {
        touches = t;
        accelometer = a;
    }
}

/// <summary>
/// Message structure for communication to NodeServer
/// </summary>
[Serializable]
public class WSMessage
{
    public int type;
    public int clientId;
    public string body;
}
#endregion

#region WebRTC Structures
/// <summary>
/// Class inherited from 
/// </summary>
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
#endregion