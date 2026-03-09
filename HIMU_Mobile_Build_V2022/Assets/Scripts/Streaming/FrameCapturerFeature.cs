using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FrameCaptureFeature : ScriptableRendererFeature
{
    FrameCapturePass pass;

    public override void Create()
    {
        pass = new FrameCapturePass();
        pass.renderPassEvent = RenderPassEvent.AfterRendering;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }
}