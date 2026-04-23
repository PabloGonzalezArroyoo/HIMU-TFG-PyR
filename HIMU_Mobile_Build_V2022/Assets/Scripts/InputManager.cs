using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    InputType[] InputEvents = { InputType.DEFAULT,
        InputType.BUTTON_UP,
        InputType.BUTTON_RIGHT,
        InputType.BUTTON_DOWN,
        InputType.BUTTON_LEFT,
        InputType.ACTION_BUTTON,
        InputType.MICROPHONE,
        InputType.PAUSE,
        InputType.START_GAME,
        InputType.END_GAME };

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


    public void AddInputEvent(int e)
    {
        DeviceInfo info = new DeviceInfo(ConnectionManager.Instance.GetDeviceInfo(), ConnectionManager.Instance.GetDeviceInfo());
        string json = JsonUtility.ToJson(new InputInfo(info, InputEvents[e]));
        ConnectionManager.Instance.SendToHost(json, 
            ConnectionManager.Instance.GetConnectionType() == ConnectionType.USB ? IPAddress.Loopback : IPAddress.Parse(ConnectionManager.Instance.GetHostIp()));
        if (InputEvents[e] == InputType.END_GAME) ConnectionManager.Instance.HandleDisconnection();
    }

    public void AddInputEvent(float v)
    {
        DeviceInfo info = new DeviceInfo(ConnectionManager.Instance.GetDeviceInfo(), ConnectionManager.Instance.GetDeviceInfo());
        string json = JsonUtility.ToJson(new InputInfo(info, v, InputType.MICROPHONE));
        ConnectionManager.Instance.SendToHost(json, 
            ConnectionManager.Instance.GetConnectionType() == ConnectionType.USB ? IPAddress.Loopback : IPAddress.Parse(ConnectionManager.Instance.GetHostIp()));
    }
}
