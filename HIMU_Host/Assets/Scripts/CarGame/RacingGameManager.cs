using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RacingGameManager : MonoBehaviour
{
    public static RacingGameManager Instance { get; private set; }

    public bool gameStarted = false;
    public bool isPaused = false;
    public bool streaming = false;
    public bool controllerConnected = false;

    public void StartGame()
    {
        RacingConnectionsUIManager.Instance.StartGame(ChangeScene);
    }

    public void EndGame()
    {
        RacingGameUIManager.Instance.EndGame(ChangeScene);
    }

    public void PauseGame()
    {
        isPaused = true;
        RacingGameUIManager.Instance.Pause();
    }

    public void ResumeGame()
    {
        isPaused = false;
        RacingGameUIManager.Instance.Resume();
    }

    public void GoToConnectionsScene()
    {
        RacingMenuUIManager.Instance.ChangeToConnections(ChangeScene);
    }

    public void ChangeScene(string sceneName)
    {
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
    }

    public void OnStreamButtonClicked()
    {
        streaming = (!streaming);
        StreamManager.Instance.FlagWebSocketServer();
        RacingConnectionsUIManager.Instance.StreamSwitched(streaming);
    }

    private void Awake()
    {
        if (Instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasReleasedThisFrame)
        {
            if(isPaused) ResumeGame();
            else PauseGame();
        }
    }
}
