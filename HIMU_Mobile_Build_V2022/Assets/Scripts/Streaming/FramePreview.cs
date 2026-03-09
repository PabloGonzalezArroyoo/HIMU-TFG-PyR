using UnityEngine;

public class FramePreview : MonoBehaviour
{
    Texture2D previewTexture;

    int width = 1280;
    int height = 720;

    void Start()
    {
        previewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        GetComponent<Renderer>().material.mainTexture = previewTexture;
    }

    void Update()
    {
        if (!FrameCaptureManager.HasNewFrame)
            return;

        previewTexture.LoadRawTextureData(FrameCaptureManager.LatestFrame);
        previewTexture.Apply();

        FrameCaptureManager.HasNewFrame = false;
    }
}