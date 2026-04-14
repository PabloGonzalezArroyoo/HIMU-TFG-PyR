using UnityEngine;

public class FramePreview : MonoBehaviour
{
    Texture2D previewTexture;

    int width = 1280;
    int height = 720;

    void Start()
    {
        previewTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        
        Renderer renderer = GetComponent<Renderer>();
        renderer.material.mainTexture = previewTexture;

        renderer.material.mainTextureScale = new Vector2(1, -1);
        renderer.material.mainTextureOffset = new Vector2(0, 1);
    }

    void Update()
    {
        //if (!FrameCaptureManager.HasNewFrame)
        //    return;

        //previewTexture.LoadRawTextureData(FrameCaptureManager.LatestFrame);
        //previewTexture.Apply();

        //FrameCaptureManager.HasNewFrame = false;
    }
}