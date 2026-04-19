using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts
{
    public enum ConnectionType
    {
        USB, WIFI // BLUETOOTH?
    }

    public enum ConnectionEvent
    {
        DEFAULT, CONNECTION, DISCONNECTION, INPUT, PAUSE, START_GAME, END_GAME
    };

    public enum InputEvent
    {
        DEFAULT, BUTTON_UP, BUTTON_DOWN, BUTTON_LEFT, BUTTON_RIGHT, ACTION_BUTTON, MICROPHONE
    };

    [System.Serializable]
    struct DeviceInfo
    {
        string deviceID;
        string deviceIP;
    }

    [System.Serializable]
    struct ConnectionInfo
    {
        ConnectionEvent connectionEvent;
        DeviceInfo infoDevice;


    };

    [System.Serializable]
    struct InputInfo
    {
        DeviceInfo infoDevice;
        InputEvent inputEvent;
        float microphoneVolume;
    };
}
