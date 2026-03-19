using NativeWebSocket;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class StreamSender
{
    static WebSocket websocket;

    static Process ffmpegProcess;
    static BinaryWriter ffmpegStdin;
    static BinaryReader ffmpegStdout;

    static int width = 1280;
    static int height = 720;

    [RuntimeInitializeOnLoadMethod]
    static async void Init()
    {
        StartFFmpeg();
        await Initialize();
    }

    [RuntimeInitializeOnLoadMethod]
    static void CreateUpdater()
    {
        var obj = new GameObject("StreamingUpdater");
        obj.AddComponent<StreamingUpdater>();
        Object.DontDestroyOnLoad(obj);
    }

    [RuntimeInitializeOnLoadMethod]
    static void RegisterQuitHandler()
    {
        Application.quitting += OnApplicationQuit;
    }

    static void OnApplicationQuit()
    {
        StopFFmpeg();
    }

    #region WebSocket
    public static async Task Initialize()
    {
        websocket = new WebSocket("ws://localhost:8080");

        websocket.OnOpen += () =>
        {
            UnityEngine.Debug.Log("WebSocket conectado");
        };

        websocket.OnError += (e) =>
        {
            UnityEngine.Debug.LogError("WebSocket error: " + e);
        };

        await websocket.Connect();
    }
    #endregion

    #region FFmpeg
    static void StartFFmpeg()
    {
        var ffmpegPath = Path.Combine(Application.dataPath, "../Tools/ffmpeg-8.1/bin/ffmpeg.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-loglevel error -f rawvideo -pix_fmt yuv420p -s {width}x{height} -i - " +
                "-c:v libx264 -preset ultrafast -tune zerolatency " +
                "-profile:v baseline -level 3.0 " +
                "-f mp4 -movflags frag_keyframe+empty_moov+default_base_moof -",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true, // Leemos H.264 codificado
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        ffmpegProcess = new Process();
        ffmpegProcess.StartInfo = startInfo;
        ffmpegProcess.ErrorDataReceived += (sender, e) => {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.Log(e.Data);
        };
        ffmpegProcess.Start();
        ffmpegProcess.BeginErrorReadLine();

        ffmpegStdin = new BinaryWriter(ffmpegProcess.StandardInput.BaseStream);
        ffmpegStdout = new BinaryReader(ffmpegProcess.StandardOutput.BaseStream);

        // Puedes leer continuamente en un Task
        Task.Run(() =>
        {
            try
            {
                byte[] buffer = new byte[1024 * 32]; // Chunk de H.264
                while (!ffmpegProcess.HasExited)
                {
                    int bytesRead = ffmpegStdout.BaseStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        UnityEngine.Debug.Log("H264");
                        byte[] chunk = new byte[bytesRead];
                        System.Array.Copy(buffer, chunk, bytesRead);
                        SendH264Chunk(chunk);
                    }
                }
            }
            catch (IOException e)
            {
                UnityEngine.Debug.LogError("FFmpeg stdout error: " + e.Message);
            }
        });
    }

    static void StopFFmpeg()
    {
        if (ffmpegProcess != null && !ffmpegProcess.HasExited)
        {
            try
            {
                // Cierra stdin para que FFmpeg termine
                ffmpegStdin?.Close();

                // Manda kill si sigue vivo después de un par de segundos
                if (!ffmpegProcess.WaitForExit(2000))
                {
                    ffmpegProcess.Kill();
                }

                ffmpegProcess.Dispose();
                ffmpegProcess = null;
                ffmpegStdin = null;
                ffmpegStdout = null;

                UnityEngine.Debug.Log("FFmpeg detenido correctamente");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("Error al detener FFmpeg: " + e.Message);
            }
        }
    }
    #endregion

    public static void SendFrame(byte[] data)
    {
        if (ffmpegProcess != null && !ffmpegProcess.HasExited)
        {
            try
            {
                ffmpegStdin.Write(data);
                ffmpegStdin.Flush();
            }
            catch (IOException e)
            {
                UnityEngine.Debug.LogError("FFmpeg stdin error: " + e.Message);
            }
        }
    }

    static async void SendH264Chunk(byte[] h264Chunk)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.Send(h264Chunk);
        }
    }

    public static void Update()
    {
        websocket?.DispatchMessageQueue();
    }
}