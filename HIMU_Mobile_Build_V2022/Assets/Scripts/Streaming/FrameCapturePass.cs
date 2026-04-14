using System.IO;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FrameCapturePass : ScriptableRenderPass
{
    RTHandle cameraColorTarget;

    public RenderTexture OutputTexture { get; private set; }

    // MODIFICAR ESTOS NÚMEROS PARA REDUCIR MB DE FRAME
    int width = 1280;
    int height = 720;

    public FrameCapturePass()
    {
        OutputTexture = new RenderTexture(width, height, 0, RenderTextureFormat.BGRA32);
        OutputTexture.enableRandomWrite = true;
        OutputTexture.Create();
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("FrameCapture");
        cmd.Blit(cameraColorTarget, OutputTexture);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void Cleanup()
    {
        OutputTexture?.Release();
    }
}