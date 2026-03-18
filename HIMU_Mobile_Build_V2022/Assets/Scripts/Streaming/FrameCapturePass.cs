using System.IO;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FrameCapturePass : ScriptableRenderPass
{
    RTHandle cameraColorTarget;

    RenderTexture yuvCaptureTexture;
    Material yuvMaterial;

    long nFrames;
    int captureEveryNFrames = 1; // [captureEveryNFrames = targetFPS / captureFPS] - 1 = 60 fps

    // MODIFICAR ESTOS NÚMEROS PARA REDUCIR MB DE FRAME
    int width = 1280;
    int height = 720;

    public FrameCapturePass()
    {
        yuvCaptureTexture = new RenderTexture(
            width,
            height + height / 2,
            0,
            RenderTextureFormat.R8
        );
        yuvCaptureTexture.Create();

        yuvMaterial = new Material(Shader.Find("Hidden/RGBToYUV420"));

        yuvMaterial.SetVector("_TexelSize", new Vector2(1.0f / width, 1.0f / height));
        yuvMaterial.SetFloat("_Height", height);

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
        cmd.Blit(cameraColorTarget, yuvCaptureTexture, yuvMaterial);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        // leer GPU sin bloquear
        AsyncGPUReadback.Request(yuvCaptureTexture, 0, request =>
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
        int ySize = width * height;
        int uvSize = ySize / 4;

        byte[] yuv = new byte[ySize + uvSize * 2];

        // Y (primer bloque)
        NativeArray<byte>.Copy(data, 0, yuv, 0, ySize);

        int uvStart = ySize;

        int uIndex = ySize;
        int vIndex = ySize + uvSize;

        int packedWidth = width;
        int packedHeight = height + height / 2;

        int uvOffset = width * height;

        for (int y = 0; y < height / 2; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte val = data[uvOffset + y * width + x];

                if (x < width / 2)
                    yuv[uIndex++] = val;
                else
                    yuv[vIndex++] = val;
            }
        }

        StreamSender.SendFrame(yuv);
    }
}