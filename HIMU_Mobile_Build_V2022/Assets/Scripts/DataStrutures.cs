using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts
{
    public enum ConnectionEvent
    {
        DEFAULT, CONNECTION, DISCONNECTION, INPUT, PAUSE, START_GAME, END_GAME
    };

    public enum InputEvent
    {
        DEFAULT, BUTTON_UP, BUTTON_DOWN, BUTTON_LEFT, BUTTON_RIGHT, ACTION_BUTTON, MICROPHONE
    };

    struct DeviceInfo
    {
        string deviceID;
        string deviceIP;
    }

    struct ConnectionInfo
    {
        ConnectionEvent connectionEvent;
        DeviceInfo infoDevice;


    };


    struct InputInfo
    {
        DeviceInfo infoDevice;
        InputEvent inputEvent;
        float microphoneVolume;
    };
}
