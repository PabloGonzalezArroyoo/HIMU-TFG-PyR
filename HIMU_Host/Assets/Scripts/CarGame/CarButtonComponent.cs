using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class CarButtonComponent : MonoBehaviour
{
    private enum CarInputEvent { ACTION, LEFT, RIGHT, PAUSE, STREAM, EXIT};

    [SerializeField]
    private CarInputEvent buttonEvent;

    private PointerComponent pointer = null;

    public bool pressed = false;

    public int buttonID { get; private set; }

    public void StartPressing()
    {
        pressed = true;
        switch (buttonEvent)
        {
            case CarInputEvent.ACTION: {
                    if (!RacingGameManager.Instance.isPaused)
                        CarController.Instance.isAccelerating = true;
                }
                break;
            case CarInputEvent.LEFT: {
                    if (!RacingGameManager.Instance.isPaused) 
                        CarController.Instance.turnLeft = true; 
                } break;
            case CarInputEvent.RIGHT: {
                    if (!RacingGameManager.Instance.isPaused)
                        CarController.Instance.turnRight = true; 
                } break;
            case CarInputEvent.PAUSE: break;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerDownHandler);
    }

    public void ReleasePressing()
    {
        switch (buttonEvent)
        {
            case CarInputEvent.ACTION: {
                    if (!RacingGameManager.Instance.isPaused)
                        CarController.Instance.isAccelerating = false;
                    else
                        pointer.ExecuteButton();
                } break;
            case CarInputEvent.LEFT: {
                    if (!RacingGameManager.Instance.isPaused)
                        CarController.Instance.turnLeft = false;
                    else
                        pointer.MoveLeft();
                } break;
            case CarInputEvent.RIGHT: { 
                    if (!RacingGameManager.Instance.isPaused) 
                        CarController.Instance.turnRight = false;
                    else
                        pointer.MoveRight();
                } break;
            case CarInputEvent.PAUSE: 
                if (RacingGameManager.Instance.gameStarted)
                {
                    CarController.Instance.StopCar();
                    RacingInputController.Instance.OnPauseButtonClick();
                }
                break;
        }
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerUpHandler);
    }

    public void Click()
    {
        switch (buttonEvent) {
            case CarInputEvent.PAUSE: RacingInputController.Instance.OnPauseButtonClick(); break;
            case CarInputEvent.STREAM: RacingGameUIManager.Instance.StreamSwitched(); break;
            case CarInputEvent.EXIT: RacingGameManager.Instance.EndGame(); break;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerClickHandler);

    }

    private void Start()
    {
        buttonID = RacingGameManager.Instance.CreateButtonID();
        pointer = RacingGameManager.Instance.GetPauseMenuPointer();
    }
}
