using Unity.Collections;

public static class FrameCaptureManager
{
    public static NativeArray<byte> LatestFrame;
    public static bool HasNewFrame = false;
}