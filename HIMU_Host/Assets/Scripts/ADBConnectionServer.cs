using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Handles phones connected by USB cable through ADB.
///
/// How the tunnel works:
/// The mobile app always connects to "localhost:remotePort" on the device itself.
/// For each connected device we run "adb -s {serial} reverse tcp:remotePort tcp:localPort",
/// assigning a DIFFERENT localPort per device. This lets adb forward that device's
/// connection to a TcpListener we own on the PC, so we can tell devices apart even
/// though everything arrives over loopback (127.0.0.1) once tunneled by adb.
///
/// From that point on, the flow mirrors SignalingServer: a framed-message handshake
/// (ConnectionData / HANDSHAKE) followed by a loop of SignalingMessage (SDP/ICE/DISCONNECT).
/// </summary>
public class ADBConnectionServer : MonoBehaviour
{
    #region Variables

    /// <summary>
    /// Port the mobile app connects to on its own localhost. Same value for every device;
    /// the per-device distinction happens on the "local" side of the reverse tunnel.
    /// </summary>
    [SerializeField]
    private int remotePort = 7778;

    /// <summary>
    /// First PC-side port available for reverse tunnels.
    /// </summary>
    [SerializeField]
    private int localPortRangeStart = 7780;

    /// <summary>
    /// Last PC-side port available. Defines how many ADB devices can be handled at once.
    /// </summary>
    [SerializeField]
    private int localPortRangeEnd = 7879;

    /// <summary>
    /// How often (ms) the background thread checks "adb devices" for changes.
    /// </summary>
    [SerializeField]
    private int devicePollIntervalMs = 2000;

    private bool running;
    private string adbPath = "";
    private Thread deviceWatcherThread;

    private readonly HashSet<int> usedLocalPorts = new HashSet<int>();

    /// <summary>
    /// Active sessions keyed by ADB serial (one per physical device).
    /// </summary>
    private readonly ConcurrentDictionary<string, DeviceSession> sessionsByDeviceId = new ConcurrentDictionary<string, DeviceSession>();

    /// <summary>
    /// Same sessions, keyed by the clientID StreamManager knows them by, so SendMessage
    /// can look them up the same way SignalingServer.SendMessage does.
    /// </summary>
    private readonly ConcurrentDictionary<string, DeviceSession> sessionsByClientID = new ConcurrentDictionary<string, DeviceSession>();

    /// <summary>
    /// Per-device state: its reverse tunnel, listener and (once the app connects) socket.
    /// </summary>
    private class DeviceSession
    {
        public string deviceId;
        public int localPort;
        public TcpListener listener;
        public Thread acceptThread;
        public TcpClient tcpClient;
        public string clientID;
    }

    #endregion

    #region ADB helpers

    /// <summary>
    /// Locates adb.exe by checking common SDK install locations.
    /// </summary>
    private string FindAdbPath()
    {
        string localAppData = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.LocalApplicationData);

        string[] candidates = {
            System.IO.Path.Combine(
                System.Environment.GetEnvironmentVariable("ANDROID_HOME") ?? "",
                "platform-tools", "adb.exe"),
            System.IO.Path.Combine(
                System.Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? "",
                "platform-tools", "adb.exe"),
            System.IO.Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe"),
            @"C:\Android\sdk\platform-tools\adb.exe",
            @"C:\android-sdk\platform-tools\adb.exe",
        };

        foreach (string path in candidates)
        {
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                UnityEngine.Debug.Log($"[ADBServer] adb found at: {path}");
                return path;
            }
        }

        UnityEngine.Debug.LogError("[ADBServer] adb.exe not found. Install Android Studio or the Android SDK.");
        return null;
    }

    /// <summary>
    /// Runs an adb command and returns its stdout.
    /// </summary>
    private string RunAdbCommand(string arguments)
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            UnityEngine.Debug.LogError("[ADBServer] adb.exe path is not set, cannot run command.");
            return "";
        }

        var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[ADBServer] Failed to run 'adb {arguments}': {ex.Message}");
            return "";
        }

        if (error.Length > 0)
            UnityEngine.Debug.LogWarning($"[ADBServer] adb {arguments} -> stderr: {error}");

        return output.ToString();
    }

    /// <summary>
    /// Parses "adb devices" output into a list of serials that are actually ready ("device" state),
    /// ignoring "unauthorized"/"offline" entries.
    /// </summary>
    private List<string> GetConnectedDeviceIds()
    {
        var ids = new List<string>();
        string output = RunAdbCommand("devices");
        if (string.IsNullOrEmpty(output)) return ids;

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("List of devices")) continue;

            string[] parts = line.Split('\t');
            if (parts.Length >= 2 && parts[1].Trim() == "device")
                ids.Add(parts[0].Trim());
        }

        return ids;
    }

    #endregion

    #region Local port pool

    private int AllocateLocalPort()
    {
        lock (usedLocalPorts)
        {
            for (int port = localPortRangeStart; port <= localPortRangeEnd; port++)
            {
                if (usedLocalPorts.Add(port))
                    return port;
            }
        }

        throw new Exception("[ADBServer] No free local ports left for adb reverse (range exhausted).");
    }

    private void ReleaseLocalPort(int port)
    {
        lock (usedLocalPorts)
            usedLocalPorts.Remove(port);
    }

    #endregion

    #region Device watcher

    /// <summary>
    /// Background loop that polls "adb devices" and reacts to devices plugging/unplugging.
    /// </summary>
    private void DeviceWatcherLoop()
    {
        while (running)
        {
            try
            {
                var current = new HashSet<string>(GetConnectedDeviceIds());

                foreach (string id in current)
                    if (!sessionsByDeviceId.ContainsKey(id))
                        OnDeviceConnected(id);

                foreach (string id in new List<string>(sessionsByDeviceId.Keys))
                    if (!current.Contains(id))
                        OnDeviceDisconnected(id);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[ADBServer] Device watcher error: {ex.Message}");
            }

            Thread.Sleep(devicePollIntervalMs);
        }
    }

    private void OnDeviceConnected(string deviceId)
    {
        var session = new DeviceSession { deviceId = deviceId };

        try
        {
            session.localPort = AllocateLocalPort();
            RunAdbCommand($"-s {deviceId} reverse tcp:{remotePort} tcp:{session.localPort}");

            session.listener = new TcpListener(IPAddress.Loopback, session.localPort);
            session.listener.Start();

            if (!sessionsByDeviceId.TryAdd(deviceId, session))
            {
                // Safety net in case the watcher loop double-fired for this device.
                session.listener.Stop();
                ReleaseLocalPort(session.localPort);
                return;
            }

            session.acceptThread = new Thread(() => AcceptLoop(session))
            {
                IsBackground = true,
                Name = $"ADB Accept {deviceId}"
            };
            session.acceptThread.Start();

            UnityEngine.Debug.Log($"[ADBServer] Device connected: {deviceId} (tcp:{remotePort} -> tcp:{session.localPort})");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[ADBServer] Failed to set up device {deviceId}: {ex.Message}");
            ReleaseLocalPort(session.localPort);
        }
    }

    private void OnDeviceDisconnected(string deviceId)
    {
        if (!sessionsByDeviceId.TryRemove(deviceId, out DeviceSession session)) return;

        UnityEngine.Debug.Log($"[ADBServer] Device disconnected: {deviceId}");

        try { session.listener?.Stop(); } catch { }
        try { session.tcpClient?.Close(); } catch { }

        // Best effort: the device is physically gone already, so this will usually just
        // fail silently, which is fine - there is nothing left to un-reverse on its side.
        RunAdbCommand($"-s {deviceId} reverse --remove tcp:{remotePort}");

        ReleaseLocalPort(session.localPort);

        if (!string.IsNullOrEmpty(session.clientID))
        {
            sessionsByClientID.TryRemove(session.clientID, out _);
            string clientID = session.clientID;
            UnityMainThreadDispatcher.Instance().Enqueue(() => StreamManager.Instance?.RemovePeer(clientID));
        }
    }

    #endregion

    #region TCP handling (mirrors SignalingServer's framed-message protocol)

    /// <summary>
    /// Waits for the phone app to connect through this device's tunnel. If the connection
    /// drops (app closed/crashed) it keeps listening in case the app reconnects, for as long
    /// as the device stays plugged in.
    /// </summary>
    private void AcceptLoop(DeviceSession session)
    {
        while (running && sessionsByDeviceId.ContainsKey(session.deviceId))
        {
            try
            {
                TcpClient tcp = session.listener.AcceptTcpClient();
                session.tcpClient = tcp;
                UnityEngine.Debug.Log($"[ADBServer] App connected through ADB tunnel for device {session.deviceId}.");
                HandleClient(session, tcp); // Blocks until this connection ends.
            }
            catch (SocketException)
            {
                break; // listener was stopped (device disconnected or server stopping)
            }
            catch (Exception ex)
            {
                if (running)
                    UnityEngine.Debug.LogWarning($"[ADBServer] Accept error for {session.deviceId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handshake + signaling loop for one device connection. Same message protocol as
    /// SignalingServer (ConnectionData handshake, then SignalingMessage for SDP/ICE/DISCONNECT).
    /// </summary>
    private void HandleClient(DeviceSession session, TcpClient tcp)
    {
        NetworkStream stream = tcp.GetStream();
        string clientID = "";

        try
        {
            if (!NetworkUtils.TryReadFramedMessage(stream, out string message))
            {
                UnityEngine.Debug.LogError($"[ADBServer] Client {session.deviceId} closed connection before handshake.");
                return;
            }

            ConnectionData decodedData = JsonUtility.FromJson<ConnectionData>(message);

            if (decodedData.connType != ConnectionEvent.HANDSHAKE)
            {
                UnityEngine.Debug.LogError("[ADBServer] Did not receive a valid handshake ConnectionData.");
                return;
            }

            clientID = Guid.NewGuid().ToString();
            session.clientID = clientID;
            sessionsByClientID.TryAdd(clientID, session);

            ClientData newClient = ClientData.ForADB(session.deviceId, clientID);
            UnityMainThreadDispatcher.Instance().Enqueue(() => StreamManager.Instance?.CreatePeerForADBClient(newClient));

            UnityEngine.Debug.Log($"[ADBServer] Client registered: {session.deviceId} (id: {clientID})");

            while (running)
            {
                if (!NetworkUtils.TryReadFramedMessage(stream, out string incoming)) break;

                var sigMsg = JsonUtility.FromJson<SignalingMessage>(incoming);

                // Runs on the main thread, same as SignalingServer, since WebRTC peers need it.
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    StreamManager.Instance?.HandleIncomingSignaling(clientID, sigMsg));
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[ADBServer] Exception handling client {session.deviceId}: {ex}");
        }
        finally
        {
            try { tcp.Close(); } catch { }
            session.tcpClient = null;

            if (!string.IsNullOrEmpty(clientID))
            {
                sessionsByClientID.TryRemove(clientID, out _);
                UnityMainThreadDispatcher.Instance().Enqueue(() => StreamManager.Instance?.RemovePeer(clientID));
            }

            session.clientID = null;
        }
    }

    /// <summary>
    /// Sends a signaling message to a specific client. Mirrors SignalingServer.SendMessage's signature
    /// so StreamManager can wire WebRTCPeer.Initialize the same way for every transport.
    /// </summary>
    public bool SendMessage(string clientID, SignalingMessage msg)
    {
        if (!sessionsByClientID.TryGetValue(clientID, out DeviceSession session) || session.tcpClient == null)
            return false;

        try
        {
            NetworkStream stream = session.tcpClient.GetStream();
            NetworkUtils.WriteFramedMessage(stream, JsonUtility.ToJson(msg), syncRoot: stream);
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[ADBServer] Error sending message to {clientID}: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Server lifecycle

    public void StartServer()
    {
        if (running) return;

        adbPath = FindAdbPath();
        if (string.IsNullOrEmpty(adbPath))
        {
            UnityEngine.Debug.LogError("[ADBServer] adb.exe not found; ADB connections will not be available.");
            return;
        }

        running = true;
        deviceWatcherThread = new Thread(DeviceWatcherLoop) { IsBackground = true, Name = "ADB Device Watcher" };
        deviceWatcherThread.Start();

        UnityEngine.Debug.Log("[ADBServer] ADB server launched.");
    }

    public void StopServer()
    {
        if (!running) return;
        running = false;

        deviceWatcherThread?.Join(devicePollIntervalMs + 500);

        foreach (string deviceId in new List<string>(sessionsByDeviceId.Keys))
            OnDeviceDisconnected(deviceId);

        UnityEngine.Debug.Log("[ADBServer] ADB server stopped.");
    }

    #endregion

    #region Monobehaviour

    void OnDestroy()
    {
        try { StopServer(); } catch { }
    }

    #endregion
}