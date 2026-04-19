using Assets.Scripts;
using Fleck;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.Android.Gradle.Manifest;
using UnityEditor.PackageManager;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
    // Info general
    public ConnectionType connectionType = ConnectionType.USB;
    public bool isGamePad = true;

    // Cosas de adb
    private string adbPath = "";
    private List<String> adbDevices = new List<String>();

    // Cosas de redes
    TcpListener server;
    WebSocketServer serverWS;

    TcpListener listener;
    TcpClient client;
    NetworkStream stream;
    Thread serverThread;
    bool running;
    bool mobileConnected = false;
    InputInfo receivedInput;
    readonly object lockObj = new object();

    UdpClient broadcaster;  //Para anunciar la IP del PC y permitir conexion luego

    #region ADB
    private string FindAdbPath()
    {
        string username = System.Environment.UserName;
        string localAppData = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.LocalApplicationData);

        string[] candidatas = {
        // Variable de entorno estándar
        System.IO.Path.Combine(
            System.Environment.GetEnvironmentVariable("ANDROID_HOME") ?? "",
            "platform-tools", "adb.exe"),
        System.IO.Path.Combine(
            System.Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? "",
            "platform-tools", "adb.exe"),

        // Ruta por defecto de Android Studio
        System.IO.Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe"),

        // Ruta si instalaste el SDK manualmente
        @"C:\Android\sdk\platform-tools\adb.exe",
        @"C:\android-sdk\platform-tools\adb.exe",
    };

        foreach (string ruta in candidatas)
        {
            if (!string.IsNullOrEmpty(ruta) && System.IO.File.Exists(ruta))
            {
                UnityEngine.Debug.Log($"adb encontrado en: {ruta}");
                return ruta;
            }
        }

        UnityEngine.Debug.LogError("No se encontró adb.exe. Instala Android Studio o el Android SDK.");
        return null;
    }

    private void ParseDeviceIds(string adbOutput)
    {
        string[] lines = adbOutput.Split('\n');

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            // Saltamos la cabecera y líneas vacías
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("List of devices"))
                continue;

            // Cada dispositivo válido tiene formato: "<id>\t<estado>"
            string[] parts = trimmed.Split('\t');
            if (parts.Length >= 2 && parts[1].Trim() == "device")
                adbDevices.Add(parts[0].Trim());
        }

        foreach (string device in adbDevices) UnityEngine.Debug.Log(device);
    }

    public string RunAdbCommand(string arguments)
    {
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

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (error.Length > 0)
            UnityEngine.Debug.LogWarning("ADB Error: " + error);

        return output.ToString();
    }

    public void ConfigureADB()
    {
        adbPath = FindAdbPath();
        string output = RunAdbCommand("devices");
        UnityEngine.Debug.Log("ADB devices Output: " + output);
        ParseDeviceIds(output);
        output = RunAdbCommand("reverse tcp:8052 tcp:8052");
        UnityEngine.Debug.Log("ADB tunnel Output: " + output);

    }
    #endregion

    #region TCP

    string GetLocalIP()
    {
        foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        return "127.0.0.1";
    }

    void Broadcast()
    {
        if (mobileConnected) return; // no envía si ya hay conexión
        string myIP = GetLocalIP();
        UnityEngine.Debug.Log(myIP);
        byte[] data = Encoding.UTF8.GetBytes("UNITY_CONTROLLER:" + myIP + ":8052");
        broadcaster.Send(data, data.Length, "255.255.255.255", 9999);
    }

    void Receive()
    {
        try
        {
            client = listener.AcceptTcpClient();
            mobileConnected = true; // el Broadcast lo leerá en el siguiente tick
            string clientID = client.Client.RemoteEndPoint.ToString();
            UnityEngine.Debug.Log("[Receiver] Móvil conectado: " + clientID);

            // Hilo independiente para cada móvil
            Thread clientThread = new Thread(() => HandleClient(client, clientID));
            clientThread.IsBackground = true;
            clientThread.Start();
        }
        catch { }
    }

    void HandleClient(TcpClient client, string clientID)
    {
        stream = client.GetStream();
        byte[] buffer = new byte[1024];

        while (running && client.Connected)
        {
            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break; // móvil desconectado

                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                InputInfo data = JsonUtility.FromJson<InputInfo>(json);
                UnityEngine.Debug.Log("[Receiver] Móvil " + clientID + " envio informacion");

                lock (lockObj)
                {
                    receivedInput = data;
                }
            }
            catch
            {
                break;
            }
        }

        client.Close();
        UnityEngine.Debug.Log("[Receiver] Móvil desconectado: " + clientID);
    }

    public void AnnounceIP()
    {
        // Anunciar la IP primero
        broadcaster = new UdpClient();
        broadcaster.EnableBroadcast = true;
        InvokeRepeating(nameof(Broadcast), 0f, 5f);

        listener = new TcpListener(IPAddress.Any, 8052);
        listener.Start();
        serverThread = new Thread(Receive);
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    public void ConfigureTCP()
    {
        AnnounceIP();
    }

    #endregion

    private void Awake()
    {
        if (connectionType == ConnectionType.USB)
        {
            ConfigureADB();
        }
        else
        {
            ConfigureTCP();
        }
    }

    void OnDestroy()
    {
        running = false;
        listener?.Stop();
        broadcaster?.Close();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        serverThread = new Thread(StartServer);
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    void StartServer()
    {
        server = new TcpListener(IPAddress.Any, 8052);
        server.Start();

        while (running)
        {
            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[1024];
            int length;

            while ((length = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                string data = Encoding.UTF8.GetString(buffer, 0, length);
            }

            client.Close();
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
