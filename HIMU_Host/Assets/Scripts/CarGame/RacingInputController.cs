using UnityEngine;
using UnityEngine.InputSystem;

public class RacingInputController : MonoBehaviour
{
    public bool paused = false;

    public void PauseGame()
    {
        paused = true;
        RacingGameManager.Instance.PauseGame();
    }

    public void ResumeGame()
    {
        paused = false;
        RacingGameManager.Instance.ResumeGame();
    }

    public void DebugText()
    {
        Debug.Log("CLICK CON RAYCAST");
    }

    private void ProcessInput()
    {

    }

    private void Update()
    {
        // Aqui proceso el input y meto la logica

        //
        if (RacingGameManager.Instance.gameStarted && Keyboard.current.escapeKey.wasReleasedThisFrame)
        {
            if (paused) ResumeGame(); 
            else PauseGame();
        }
    }
}
