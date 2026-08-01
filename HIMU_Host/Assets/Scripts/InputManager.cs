using NUnit.Framework;
using UnityEngine;
using Unity.WebRTC;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Tiene una referencia al canal de input para procesar la informacion que llega por ese canal (la convierte a eventos de input accesibles)
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    [SerializeField]
    private bool shouldPersist = false;

    public List<InputFrame> pendingInputFrames = new List<InputFrame>();

    public void ProcessInputMessage(string data)
    {
        InputFrame frame = JsonUtility.FromJson<InputFrame>(data);
        pendingInputFrames.Add(frame);
    }

    private void Awake()
    {
        if (Instance) { DestroyImmediate(gameObject); return; }
        Instance = this;
        if(shouldPersist) DontDestroyOnLoad(gameObject);
    }

    // Limpiamos en el momento del frame para no borrar input antes de procesarlo
    private void LateUpdate()
    {
        pendingInputFrames.Clear();
    }
}
