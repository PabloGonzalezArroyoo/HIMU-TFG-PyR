using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    /// <summary>
    /// Component incharged of the communication through the WebRTC protcol with the host machine.
    /// </summary>
    private WebRTCReceiver receiver;

    /// <summary>
    /// Touches in the previous frame.
    /// </summary>
    private int previousTouchCount;

    public bool send = false;

    void Awake()
    {
        if (Instance) { DestroyImmediate(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        previousTouchCount = 0;
        receiver = GetComponent<WebRTCReceiver>();
        send = false;
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

    // Update is called once per frame
    void Update()
    {
        if (!send || receiver == null) return;

        var activeTouches = Touch.activeTouches;
        int count = activeTouches.Count;

        List<Vector2> touches = new List<Vector2>();
        Vector3 accValue = Accelerometer.current.acceleration.ReadValue();
        InputFrame frameInput = new InputFrame(touches, accValue);

        if (count == 0 && previousTouchCount == 0)
        {
            receiver.SendThroughDataChannel(JsonUtility.ToJson(frameInput));
            return; 
        }

        for (int i = 0; i < count; i++)
        {
            Touch t = activeTouches[i];
            Vector2 normalizedPos = new Vector2(t.screenPosition.x / Screen.width, t.screenPosition.y / Screen.height);
            touches.Add(normalizedPos);
        }

        receiver.SendThroughDataChannel(JsonUtility.ToJson(frameInput));
        previousTouchCount = count;
    }
}
