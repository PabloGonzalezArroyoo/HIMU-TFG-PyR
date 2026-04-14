using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FrameCaptureFeature : ScriptableRendererFeature
{
    public static FrameCaptureFeature Instance { get; private set; }

    FrameCapturePass pass;
    public RenderTexture CapturedFrame => pass?.OutputTexture;

    public override void Create()
    {
        Instance = this;
        pass = new FrameCapturePass();
        pass.renderPassEvent = RenderPassEvent.AfterRendering;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!Application.isPlaying)
            return;

        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Cleanup();
    }
}