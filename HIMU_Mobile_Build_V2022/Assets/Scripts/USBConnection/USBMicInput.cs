using UnityEngine;

public class MicInput : MonoBehaviour
{
    AudioClip clip;
    float[] samples = new float[128];

    void Start()
    {
        clip = Microphone.Start(null, true, 10, 44100);
    }

    public float GetVolume()
    {
        int pos = Microphone.GetPosition(null) - 128;
        if (pos < 0) return 0;
        clip.GetData(samples, pos);
        float sum = 0;
        foreach (var s in samples) sum += s * s;
        return Mathf.Sqrt(sum / samples.Length); // RMS
    }
}