using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Parses input events received from clients. It stores them into a list that is accessible for other scripts and flushes the events at lateUpdate (all processing must be done in update)
/// </summary>
public class InputManager : MonoBehaviour
{

    #region Variables

    /// <summary>
    /// Instance of InputManager (Singleton)
    /// </summary>
    public static InputManager Instance { get; private set; }

    /// <summary>
    /// Defines whether this scripts object persists between scenes or not
    /// </summary>
    [SerializeField]
    private bool shouldPersist = false;

    /// <summary>
    /// Time in seconds after which a client's input state stops being considered valid.
    /// </summary>
    [SerializeField]
    private float inputTimeout = 0.5f;

    /// <summary>
    /// Dictionary of events pending of processing (accessible)
    /// </summary>
    private readonly Dictionary<string, InputFrame> pendingInputFrames = new Dictionary<string, InputFrame>();

    #endregion

    #region Methods

    /// <summary>
    /// Parses data received from client
    /// </summary>
    /// <param name="data"></param>
    public void ParseInputMessage(string clientID, string data)
    {
        InputFrame frame;
        try
        {
            frame = JsonUtility.FromJson<InputFrame>(data);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[InputManager] Malformed input frame from " + clientID + ":" + e.Message);
            return;
        }

        pendingInputFrames[clientID] = frame;
    }

    public void RemoveClient(string clientID)
    {
        if (string.IsNullOrEmpty(clientID)) return;
        pendingInputFrames.Remove(clientID);
    }

    #endregion

    #region Getters

    /// <summary>
    /// Access to pending InputFrames list
    /// </summary>
    /// <returns></returns>
    public InputFrame GetInputFrame(string clientID)
    {
        InputFrame frame = GetValidFrame(clientID);
        return frame != null ? frame : null;
    }

    private InputFrame GetValidFrame(string clientID)
    {
        if (string.IsNullOrEmpty(clientID)) return null;
        if (!pendingInputFrames.TryGetValue(clientID, out InputFrame frame)) return null;
        if (Time.unscaledTime - frame.timestamp > inputTimeout) return null;

        return frame;
    }

    #endregion

    #region Monobehaviour

    private void Awake()
    {
        if (Instance) { DestroyImmediate(gameObject); return; }
        Instance = this;
        if(shouldPersist) DontDestroyOnLoad(gameObject);
    }

    private void LateUpdate()
    {
        pendingInputFrames.Clear();
    }

    #endregion
}
