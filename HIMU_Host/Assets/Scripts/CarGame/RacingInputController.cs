using UnityEngine;

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
}
