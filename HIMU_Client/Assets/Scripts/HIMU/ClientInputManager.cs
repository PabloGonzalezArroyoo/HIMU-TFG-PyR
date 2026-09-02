using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Class in charge of capturing this device's input (touches and accelerometer) and sending it
/// to the host through the DataChannel
/// </summary>
public class ClientInputManager : MonoBehaviour
{

    #region Variables
    /// <summary>
    /// Instance of InputManager (Singleton)
    /// </summary>
    public static ClientInputManager Instance { get; private set; }

    /// <summary>
    /// Frequency in which input frames are sent to the host, in hz.
    /// </summary>
    [SerializeField]
    private float sendRateHz = 60f;

    /// <summary>
    /// Frequency in which the accelerometer changes will be registered in hz.
    /// </summary>
    [SerializeField] private float accSamplingHz = 60f;

    /// <summary>
    /// Component incharged of the communication through the WebRTC protcol with the host machine.
    /// </summary>
    private HIMUReceiver receiver;

    /// <summary>
    /// Accelerometer reference (null if it doesn't exist).
    /// </summary>
    private Accelerometer accelerometer;

    /// <summary>
    /// Input buffer in an already allocated structure.
    /// </summary>
    private InputFrame inputFrame = new InputFrame();

    /// <summary>
    /// Whether InputManager is sending or not input to the host machine.
    /// </summary>
    public bool send = false;

    /// <summary>
    /// Reused touch position in normalized coordinates, so no allocation is made per touch.
    /// </summary>
    private Vector2 normalizedPos;

    /// <summary>
    /// Variables for sending frequency control.
    /// </summary>
    private float sendInterval;
    private float nextSendTime;
    #endregion

    #region Methods
    /// <summary>
    /// Enables EnhancedTouch (API for easy tracking of touches on a screen) and the accelerometer, due to both of them being
    /// off by default. It also configures the accelerometer sampling frequency.
    /// </summary>
    private void EnablePhoneInput()
    {
        // Enhanced Touch ---
        EnhancedTouchSupport.Enable();

        // Accelerometer ---
        accelerometer = Accelerometer.current;

        if (accelerometer == null)
        {
            Debug.LogWarning("[InputManager] No accelerometer detected.");
            return;
        }

        if (!accelerometer.enabled)
        {
            InputSystem.EnableDevice(accelerometer);

            if (accSamplingHz > 0f)
                accelerometer.samplingFrequency = accSamplingHz;
        }
    }

    /// <summary>
    /// Disable the previous enabled input channels.
    /// </summary>
    private void DisablePhoneInput()
    {
        // Enhanced Touch ---
        EnhancedTouchSupport.Disable();

        // Accelerometer ---
        if (accelerometer != null && accelerometer.enabled)
            InputSystem.DisableDevice(accelerometer);
    }
    #endregion

    #region Monobehaviour
    private void OnEnable()
    {
        EnablePhoneInput();
    }

    private void OnDisable()
    {
        DisablePhoneInput();
    }

    void Awake()
    {
        if (Instance)
        {
            try { Destroy(Instance.gameObject); }
            catch { }
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        receiver = GetComponent<HIMUReceiver>();
        sendInterval = sendRateHz > 0f ? 1f / sendRateHz : 0f;
        nextSendTime = 0f;
        send = false;
        normalizedPos = Vector2.zero;
    }

    void Update()
    {
        if (!send || receiver == null) return;

        // We limit the sending frequency
        if (sendInterval > 0f)
        {
            if (Time.unscaledTime < nextSendTime) return;
            nextSendTime = Time.unscaledTime + sendInterval;
        }

        // Touches
        inputFrame.touches.Clear();

        var activeTouches = Touch.activeTouches;
        int count = activeTouches.Count;

        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                Touch t = activeTouches[i];
                // Positions are normalized so the host can map them into its own resolution
                normalizedPos.x = t.screenPosition.x / Screen.width;
                normalizedPos.y = t.screenPosition.y / Screen.height;
                inputFrame.touches.Add(new TouchData(normalizedPos));
            }
        }

        // Accelerometer
        Vector3 accValue = Vector3.zero;
        if (accelerometer != null)
            accValue = accelerometer.acceleration.ReadValue();
        inputFrame.accelerometer = accValue;

        // Sending
        inputFrame.sentAt = Time.unscaledTime;
        receiver.SendThroughDataChannel(JsonUtility.ToJson(inputFrame));
    }
    #endregion

}
