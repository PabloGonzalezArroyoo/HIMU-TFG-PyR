using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [SerializeField]
    private bool shouldPersist = false;

    /// <summary>
    /// Component incharged of the communication through the WebRTC protcol with the host machine.
    /// </summary>
    private WebRTCReceiver receiver;

    /// <summary>
    /// Touches in the previous frame.
    /// </summary>
    private int previousTouchCount;

    /// <summary>
    /// Sets the receiver. This exists because the receiver is created dynamically once the connection
    /// with the host has been established, therefore it should be called after those instructions.
    /// </summary>
    /// <param name="r">Active receiver where the data is sent to the host.</param>
    public void SetReceiver(WebRTCReceiver r)
    {
        receiver = r;
    }

    void Awake()
    {
        if (Instance) { DestroyImmediate(gameObject); return; }
        Instance = this;
        if (shouldPersist) DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // EnhancedTouch is disabled by default (perf reasons), so it must be enabled explicitly.
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        previousTouchCount = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (receiver == null) return;

        var activeTouches = Touch.activeTouches;
        int count = activeTouches.Count;

        // Nothing to send -> there weren't and there aren't any touches
        if (count == 0 && previousTouchCount == 0) return;
        List<Vector2> touches = new List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            Touch t = activeTouches[i];
            Vector2 normalizedPos = new Vector2(t.screenPosition.x / Screen.width, t.screenPosition.y / Screen.height);
            touches[i] = normalizedPos;
        }

        Vector3 accValue = Accelerometer.current.acceleration.ReadValue();

        InputFrame frameInput = new InputFrame(touches, accValue);
        receiver.SendThroughDataChannel(JsonUtility.ToJson(frameInput));
        previousTouchCount = count;
    }
}
