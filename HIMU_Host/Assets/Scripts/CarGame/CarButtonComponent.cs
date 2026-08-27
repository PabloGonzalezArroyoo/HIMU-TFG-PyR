using System;
using UnityEngine;

public class CarButtonComponent : MonoBehaviour
{
    private enum CarInputEvent { ACTION, LEFT, RIGHT, PAUSE};

    [SerializeField]
    private CarInputEvent buttonEvent;

    public bool pressed { get; private set; } = false;

    public int buttonID { get; private set; }

    public void StartPressing()
    {
        // switch segun el evento hago tal o pascual
        pressed = true;

        switch (buttonEvent)
        {
            case CarInputEvent.ACTION: break;
            case CarInputEvent.LEFT: break;
            case CarInputEvent.RIGHT: break;
            default: break;
        }
    }

    public void ReleasePressing()
    {
        // switch segun el evento hago tal o pascual
    }

    private void Start()
    {
        buttonID = RacingGameManager.Instance.CreateButtonID();
    }

    private void LateUpdate()
    {
        pressed = false;
    }
}
