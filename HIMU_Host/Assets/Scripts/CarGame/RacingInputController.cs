using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RacingInputController : MonoBehaviour
{
    public static RacingInputController Instance { get; private set; }

    public bool paused = false;
    public bool connected = true;

    public GraphicRaycaster backgroundRaycaster;

    private Dictionary<int, CarButtonComponent> previousButtonsPressed = new Dictionary<int, CarButtonComponent>();

    private List<CarButtonComponent> buttonsPressed = new List<CarButtonComponent>();

    public void OnPauseButtonClick()
    {
        paused = (!paused);
        if (paused)
        {
            RacingGameUIManager.Instance.Pause();
            RacingGameManager.Instance.PauseGame();
        }
        else
        {
            RacingGameUIManager.Instance.Resume();
            RacingGameManager.Instance.ResumeGame();
        }
    }

    private void ProcessInput()
    {
        foreach (var button in previousButtonsPressed)
        {
            button.Value.pressed = false;
        }

        InputFrame inputEvent = HostInputManager.Instance.GetInputFrame(RacingGameManager.Instance.GetPlayerID());

        if (inputEvent == null) return;

        foreach (TouchData touch in inputEvent.touches) {
            TryClickBackgroundUI(touch.x, touch.y);
        }

        List<CarButtonComponent> buttonsToRemove = new List<CarButtonComponent>();
        foreach (var button in previousButtonsPressed)
        {
            if (!button.Value.pressed)
            {
                button.Value.ReleasePressing();
                buttonsToRemove.Add(button.Value);
            }
        }

        for (int i = 0; i < buttonsToRemove.Count; i++) { 
            previousButtonsPressed.Remove(buttonsToRemove[i].buttonID);
        }
        buttonsToRemove.Clear();

        for (int i = 0; i < buttonsPressed.Count; i++)
        {
            previousButtonsPressed.Add(buttonsPressed[i].buttonID, buttonsPressed[i]);
        }
        buttonsPressed.Clear();
    }

    /// <summary>
    /// Lanza un raycast al recibir un vector posicion que representa un click o toque de un cliente
    /// </summary>
    /// <param name="screenPosition">Posicion del click donde debemos lanzar el raycast (viene ya normalizada)</param>
    private void TryClickBackgroundUI(float screenPositionX, float screenPositionY)
    {
        if (backgroundRaycaster == null)
        {
            Debug.Log("No hay referencia al raycaster del canvas");
            return;
        }

        // Desnormalizamos las coordenadas del click
        Vector2 virtualScreenPos = new Vector2(screenPositionX * Screen.width, screenPositionY * Screen.height);

        // Lanzamos el raycast
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = virtualScreenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        backgroundRaycaster.Raycast(pointerData, results);

        // Recorremos todos los objetos que detecta el raycast
        if (results.Count > 0)
        {
            for (int i = 0; i < results.Count; i++)
            {
                CarButtonComponent carButton = null;
                carButton = results[i].gameObject.GetComponent<CarButtonComponent>();
                if (carButton != null)
                {
                    if (!previousButtonsPressed.ContainsKey(carButton.buttonID))
                    {
                        carButton.StartPressing();
                        buttonsPressed.Add(carButton);
                    }
                    else 
                        previousButtonsPressed.GetValueOrDefault(carButton.buttonID).pressed = true;
                }
            }
        }
    }

    private IEnumerator CheckPhoneConnected()
    {
        while (connected && RacingGameManager.Instance.gameStarted)
        {
            connected = StreamManager.Instance.GetADBClients().Count > 0;
            yield return null;
        }

        if (!connected)
        {
            RacingGameUIManager.Instance.ShowDisconnectionText();
            RacingGameUIManager.Instance.ExitGame();
        }
    }

    private void Awake()
    {
        if (Instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(CheckPhoneConnected());
    }

    private void Update()
    {
        ProcessInput();
    }
}
