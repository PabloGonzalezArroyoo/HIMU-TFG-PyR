using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Custom ScriptableRendarPass that runs as part of the URP rendering pipeline. It captures
/// the camera's rendered frame into a RenderTexture that will be available to WebRTC to be
/// used for streaming.
/// </summary>
public class FrameCapturePass : ScriptableRenderPass
{
    #region Variables
    /// <summary>
    /// Handle to the camera's color buffer, obtained each frame during camera setup.
    /// </summary>
    RTHandle cameraColorTarget;

    /// <summary>
    /// The captured frame. Accesible externally for WebRTC streaming.
    /// </summary>
    public RenderTexture OutputTexture { get; private set; }

    /// <summary>
    /// Width of the captured frame (reduce to lower streaming bandwith)
    /// </summary>
    int width = 1280;

    /// <summary>
    /// Height of the captured frame (reduce to lower streaming bandwith)
    /// </summary>
    int height = 720;
    #endregion

    #region Methods
    /// <summary>
    /// Initializes the pass by creating the output RenderTexture with the defined values.
    /// </summary>
    public FrameCapturePass()
    {
        // BGRA32 -> format expected by WebRTC
        OutputTexture = new RenderTexture(width, height, 0, RenderTextureFormat.BGRA32);

        // Allows the GPU to write on the texture if needed (like in Blit operations)
        OutputTexture.enableRandomWrite = true;

        OutputTexture.Create();
    }


    /// <summary>
    /// Called by URP everytime before the pass executes (= every frame). Sets the camera's current
    /// color buffer handle to be used in Execute()'s capturing frame operations.
    /// </summary>
    /// <param name="cmd">Command buffer provided by URP (unused)</param>
    /// <param name="renderingData">Rendering data containing the camera's buffer handles</param>
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
    }


    /// <summary>
    /// Called by URP during the render loop. Copies the camera's color buffer into the OutputTexture
    /// via the Blit command, making the frame available for WebRTC streaming.
    /// </summary>
    /// <param name="context">The render context used to submit GPU commands</param>
    /// <param name="renderingData">Rendering data for the current frame</param>
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        // Gets a CommandBuffer from the pool to avoid creating new objects each frame
        // NOTE: Unity saves a set of commands so that this problem is mitigated, that's why we
        // call the CommandBufferPool.
        CommandBuffer cmd = CommandBufferPool.Get("FrameCapture");

        // Copies (blits) the camera's rendered frame into the output RenderTexture
        cmd.Blit(cameraColorTarget, OutputTexture);

        // Submits the command to the GPU for execution
        context.ExecuteCommandBuffer(cmd);

        // Returns the CommandBuffer to the pool for reuse, so that memory is reused instead of
        // created and destroyed every frame
        CommandBufferPool.Release(cmd);
    }


    /// <summary>
    /// Relases the memory allocated by OutputTexture when the pass is no longer needed
    /// </summary>
    public void Cleanup()
    {
        OutputTexture?.Release();
    }
    #endregion
}