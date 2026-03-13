using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FrameCapturePass : ScriptableRenderPass
{
    RTHandle cameraColorTarget;

    RenderTexture captureTexture;
    Texture2D jpgTexture;

    long nFrames;
    int captureEveryNFrames = 1; // [captureEveryNFrames = targetFPS / captureFPS] - 1 = 60 fps

    bool saveFrame;

    // MODIFICAR ESTOS NÚMEROS PARA REDUCIR MB DE FRAME
    int width = 1280;
    int height = 720;

    public FrameCapturePass()
    {
        captureTexture = new RenderTexture(
            width,
            height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB
        );

        captureTexture.Create();

        jpgTexture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false);

        saveFrame = false;
        nFrames = 0;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (Time.frameCount % captureEveryNFrames != 0)
            return;

        CommandBuffer cmd = CommandBufferPool.Get("FrameCapture");

        // copiar frame a textura de captura
        cmd.Blit(cameraColorTarget, captureTexture);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        // leer GPU sin bloquear
        AsyncGPUReadback.Request(captureTexture, 0, request =>
        {
            if (request.hasError)
            {
                Debug.LogError("GPU readback error");
                return;
            }

            var data = request.GetData<byte>();

            ProcessFrame(data);

            FrameCaptureManager.LatestFrame = data;
            FrameCaptureManager.HasNewFrame = true;
        });
    }

    private void ProcessFrame(NativeArray<byte> data)
    {
        if (jpgTexture != null)
        {
            jpgTexture.LoadRawTextureData(data);
            jpgTexture.Apply();

            byte[] jpg = jpgTexture.EncodeToJPG(75);

            if (saveFrame)
            {
                string path = Application.dataPath + "/frame_capture.jpg";
                System.IO.File.WriteAllBytes(path, jpg);
                Debug.Log("Frame guardado en: " + path);
            }

            StreamSender.SendFrame(jpg);

            nFrames++;
            Debug.Log("Número de frames: " + nFrames);
        }
    }
}