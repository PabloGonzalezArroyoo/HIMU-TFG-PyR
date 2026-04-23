using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts
{
    public enum ConnectionType
    {
        USB, WIFI // BLUETOOTH?
    }

    public enum ConnectionEvent
    {
        DEFAULT, CONNECTION, DISCONNECTION, INPUT, START_GAME, END_GAME
    };

    public enum InputType
    {
        DEFAULT, BUTTON_UP, BUTTON_DOWN, BUTTON_LEFT, BUTTON_RIGHT, ACTION_BUTTON, MICROPHONE, PAUSE
    };

    [System.Serializable]
    public struct DeviceInfo
    {
        public string deviceID;
        public string deviceIP;

        public DeviceInfo(string id, string ip)
        {
            deviceID = id;
            deviceIP = ip;
        }
    }

    [System.Serializable]
    struct ConnectionInfo
    {
        ConnectionEvent connectionEvent;
        DeviceInfo infoDevice;
    };

    [System.Serializable]
    public struct InputInfo
    {
        string deviceIdentifier;
        InputType inputEvent;
        float microphoneVolume;

        public InputInfo(string info, InputType e)
        {
            deviceIdentifier = info;
            inputEvent = e;
            microphoneVolume = 0f;
        }
    };
}
