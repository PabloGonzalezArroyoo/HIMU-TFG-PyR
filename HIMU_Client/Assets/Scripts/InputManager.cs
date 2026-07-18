using UnityEngine;

public class InputManager : MonoBehaviour
{
    #region Variables

    /// <summary>
    /// Component incharged of the communication through the WebRTC protcol with the host machine.
    /// </summary>
    private WebRTCReceiver receiver;

    /// <summary>
    /// Touches in the previous frame.
    /// </summary>
    private int previousTouchCount;

    #endregion

    #region Setters

    /// <summary>
    /// Sets the receiver. This exists because the receiver is created dynamically once the connection
    /// with the host has been established, therefore it should be called after those instructions.
    /// </summary>
    /// <param name="r">Active receiver where the data is sent to the host.</param>
    public void SetReceiver(WebRTCReceiver r)
    {
        receiver = r;
    }

    #endregion

    #region Monobehaviour

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        previousTouchCount = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (receiver != null) return;

        int count = Input.touchCount;

        // Nothing to send -> there weren't and there aren't any touches
        if (count == 0 && previousTouchCount == 0) return;

        TouchesData[] touches = new TouchesData[count];
        for (int i = 0; i < count; i++)
        {
            Touch t = Input.GetTouch(i);
            touches[i] = new TouchesData(t.fingerId, t.position);
        }

        InputFrame frameInput = new InputFrame(touches);
        receiver.SendThroughDataChannel(JsonUtility.ToJson(frameInput));
        previousTouchCount = count;
    }

    #endregion
}
