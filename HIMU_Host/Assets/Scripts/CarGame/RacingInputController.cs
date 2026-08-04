using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RacingInputController : MonoBehaviour
{
    public static RacingInputController Instance { get; private set; }

    public bool paused = false;
    public bool connected = true;

    public void PauseGame()
    {
        paused = true;
        RacingGameUIManager.Instance.Pause();
        RacingGameManager.Instance.PauseGame();
    }

    public void ResumeGame()
    {
        paused = false;
        RacingGameUIManager.Instance.Resume();
        RacingGameManager.Instance.ResumeGame();
    }

    private void ProcessInput()
    {

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
        //StartCoroutine(CheckPhoneConnected());
    }

    private void Update()
    {
        // Aqui proceso el input y meto la logica

        // Para la pausa
        if (RacingGameManager.Instance.gameStarted && Keyboard.current.escapeKey.wasReleasedThisFrame)
        {
            if (paused) ResumeGame(); 
            else PauseGame();
        }
    }
}
