using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEditorInternal.ReorderableList;

namespace Assets.Scripts
{
    public enum ConnectionType
    {
        USB, WIFI // BLUETOOTH?
    }

    public enum ConnectionEvent
    {
        DEFAULT, CONNECTION, DISCONNECTION
    };

    public enum InputType
    {
        DEFAULT, BUTTON_UP, BUTTON_RIGHT, BUTTON_DOWN, BUTTON_LEFT, ACTION_BUTTON, MICROPHONE, PAUSE, START_GAME, END_GAME
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
    public struct ConnectionInfo
    {
        public ConnectionEvent connectionEvent;
        public DeviceInfo infoDevice;

        public ConnectionInfo(ConnectionEvent e, DeviceInfo d)
        {
            connectionEvent = e;
            infoDevice = d;
        }
    };

    [System.Serializable]
    public struct InputInfo
    {
        public DeviceInfo deviceIdentifier;
        public InputType inputEvent;
        public float microphoneVolume;

        public InputInfo(DeviceInfo info, InputType e)
        {
            deviceIdentifier = info;
            inputEvent = e;
            microphoneVolume = 0f;
        }

        public InputInfo(DeviceInfo info, float v, InputType e)
        {
            deviceIdentifier = info;
            inputEvent = e;
            microphoneVolume = v;
        }
    };
}
