using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FrameCapturePass : ScriptableRenderPass
{
    RTHandle cameraColorTarget;

    RenderTexture captureTexture;

    int width = 1280;
    int height = 720;

    public FrameCapturePass()
    {
        captureTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        captureTexture.Create();
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
        cmd.Blit(cameraColorTarget, captureTexture, new Vector2(1, -1), new Vector2(0, 1));

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

            FrameCaptureManager.LatestFrame = request.GetData<byte>();
            FrameCaptureManager.HasNewFrame = true;
        });
    }
}