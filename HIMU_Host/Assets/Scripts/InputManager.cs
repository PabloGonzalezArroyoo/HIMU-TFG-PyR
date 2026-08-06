using NUnit.Framework;
using UnityEngine;
using Unity.WebRTC;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Parses input events received from clients. It stores them into a list that is accessible for other scripts and flushes the events at lateUpdate (all processing must be done in update)
/// </summary>
public class InputManager : MonoBehaviour
{
    /// <summary>
    /// Instance of InputManager (Singleton)
    /// </summary>
    public static InputManager Instance { get; private set; }

    /// <summary>
    /// Defines whether we print information or not
    /// </summary>
    [SerializeField]
    private bool debug = false;

    /// <summary>
    /// Defines whether this scripts object persists between scenes or not
    /// </summary>
    [SerializeField]
    private bool shouldPersist = false;

    /// <summary>
    /// List of events pending of processing (accessible)
    /// </summary>
    private List<InputFrame> pendingInputFrames = new List<InputFrame>();

    /// <summary>
    /// Parses data received from client
    /// </summary>
    /// <param name="data"></param>
    public void ParseInputMessage(string data)
    {
        InputFrame frame = JsonUtility.FromJson<InputFrame>(data);
        pendingInputFrames.Add(frame);
    }

    /// <summary>
    /// Access to pending InputFrames list
    /// </summary>
    /// <returns></returns>
    public List<InputFrame> GetPendingInputFrames()
    {
        return pendingInputFrames;
    }

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
}
