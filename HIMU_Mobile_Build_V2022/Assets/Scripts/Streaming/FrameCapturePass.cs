using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FrameCapturePass : ScriptableRenderPass
{
    RTHandle cameraColorTarget;

    RenderTexture captureTexture;

    bool frameDone;
    float time;

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

        time = 0;
        frameDone = false;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
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

            SaveToDisk(data);

            FrameCaptureManager.LatestFrame = data;
            FrameCaptureManager.HasNewFrame = true;
        });
    }

    private void SaveToDisk(NativeArray<byte> data)
    {
        time += Time.deltaTime;
        if (time > 2 && !frameDone)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(data);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();

            string path = Application.dataPath + "/frame_capture.png";
            System.IO.File.WriteAllBytes(path, png);

            Debug.Log("Frame guardado en: " + path);

            frameDone = true;
        }
    }
}