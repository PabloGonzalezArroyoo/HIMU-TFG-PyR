using UnityEngine;
using System.Collections;

public class FramePreview : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitUntil(() => FrameCaptureFeature.Instance?.CapturedFrame != null);

        RenderTexture rt = FrameCaptureFeature.Instance.CapturedFrame;

        Material mat = GetComponent<Renderer>().material;
        mat.mainTexture = rt;

        // Las RenderTextures están volteadas verticalmente respecto a la UV del plano
        mat.mainTextureScale = new Vector2(1, -1);
        mat.mainTextureOffset = new Vector2(0, 1);
    }
}