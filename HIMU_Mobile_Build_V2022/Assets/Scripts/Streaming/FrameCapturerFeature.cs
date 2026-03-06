using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FrameCapturerFeature : ScriptableRendererFeature
{
    class FrameCapturePass : ScriptableRenderPass, IDisposable
    {
        private RTHandle cameraColorTarget;
        private RenderTexture captureTexture;
        private Texture2D previewTexture;
        private Renderer previewRenderer;

        private int width = 1280;
        private int height = 720;

        public FrameCapturePass(Renderer previewRenderer, int width, int height)
        {
            this.previewRenderer = previewRenderer;
            this.width = width;
            this.height = height;

            // Crear Textura de preview
            previewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            if (previewRenderer != null)
                previewRenderer.material.mainTexture = previewTexture;

            // RenderTexture para lectura
            captureTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            captureTexture.Create();
        }

        public void Setup(RTHandle cameraColorTarget)
        {
            this.cameraColorTarget = cameraColorTarget;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Copiamos el color final de la cámara a nuestro RenderTexture
            CommandBuffer cmd = CommandBufferPool.Get("FrameCapturePass");
            cmd.Blit(cameraColorTarget, captureTexture);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // Leer el frame en CPU (AsyncGPUReadback)
            AsyncGPUReadback.Request(captureTexture, 0, request =>
            {
                if (!request.hasError)
                {
                    var data = request.GetData<byte>();
                    previewTexture.LoadRawTextureData(data.ToArray());
                    previewTexture.Apply();
                }
            });
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(captureTexture);
            captureTexture = null;
        }
    }

    public Renderer previewRenderer;
    public int captureWidth = 1280;
    public int captureHeight = 720;

    FrameCapturePass frameCapturePass;

    public override void Create()
    {
        frameCapturePass = new FrameCapturePass(previewRenderer, captureWidth, captureHeight)
        {
            renderPassEvent = RenderPassEvent.AfterRendering
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        frameCapturePass.Setup(renderer.cameraColorTargetHandle);
        renderer.EnqueuePass(frameCapturePass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        frameCapturePass.Dispose();
    }
}
