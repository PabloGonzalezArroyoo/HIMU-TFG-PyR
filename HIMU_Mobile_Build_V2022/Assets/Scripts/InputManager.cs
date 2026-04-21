using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    InputType[] InputEvents = { InputType.DEFAULT,
        InputType.BUTTON_UP,
        InputType.BUTTON_RIGHT,
        InputType.BUTTON_DOWN,
        InputType.BUTTON_LEFT,
        InputType.ACTION_BUTTON,
        InputType.PAUSE,
        InputType.MICROPHONE };

    public void AddInputEvent(int e)
    {
        ConnectionManager.Instance.EnviarDatos(new InputInfo(ConnectionManager.Instance.GetDeviceInfo(), InputEvents[e]));
    }
}
