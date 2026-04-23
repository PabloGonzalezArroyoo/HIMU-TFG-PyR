using Assets.Scripts;
using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    [SerializeField]
    private PlayerController player;
    [SerializeField]
    private BalloonInputController balloonInputController;

    // Singleton
    public static InputManager Instance
    {
        get
        {
            return instance;
        }
    }
    private static InputManager instance = null;

    private void Awake()
    {
        if (instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        instance = this;
    }

    public void OnInputReceived(string device, InputInfo message)
    {
        UnityEngine.Debug.Log($"[Host] Mensaje de {device}: {message}");
        switch (message.inputEvent) {
            case InputType.BUTTON_UP: player?.AddDirection(0, 1); break;
            case InputType.BUTTON_RIGHT: player?.AddDirection(1, 0); break;
            case InputType.BUTTON_DOWN: player?.AddDirection(0, -1); break;
            case InputType.BUTTON_LEFT: player?.AddDirection(-1, 0); break;
            case InputType.ACTION_BUTTON: player?.Jump(); break;
            case InputType.MICROPHONE: balloonInputController?.SetVolume(message.microphoneVolume); break;
            case InputType.END_GAME: ConnectionManager.Instance.HandleDisconnection(); break;
        }
    }
}
