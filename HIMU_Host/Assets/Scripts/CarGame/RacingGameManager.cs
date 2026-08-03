using System.Collections;
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

    private int recordTime = 0;

    public int GetScore()
    {
        return recordTime;
    }

    public void SetScore(int record)
    {
        recordTime = record;
    }

    public void LoadRemoteControlScene(Scene current, Scene next)
    {
        if (next.name != "RacingGame_MainScene") return;

        SceneManager.LoadScene("RacingGame_RemoteControlScene", LoadSceneMode.Additive);
    }


    public void OnGameStarted(Scene loadedScene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Additive) return;

        Camera backgroundCamera = FindCameraInScene(loadedScene);
        StreamManager.Instance.SetStreamCamera(backgroundCamera);
    }

    private Camera FindCameraInScene(UnityEngine.SceneManagement.Scene scene)
    {
        // Recorremos los objetos raíz de la escena buscando la cámara
        foreach (GameObject rootObj in scene.GetRootGameObjects())
        {
            // Si se especificó un nombre concreto, priorizamos búsqueda exacta
            if (!string.IsNullOrEmpty("RemoteControl_Camera"))
            {
                if (rootObj.name == "RemoteControl_Camera")
                {
                    Camera cam = rootObj.GetComponent<Camera>();
                    if (cam != null) return cam;
                }

                Transform found = rootObj.transform.Find("RemoteControl_Camera");
                if (found != null)
                {
                    Camera cam = found.GetComponent<Camera>();
                    if (cam != null) return cam;
                }
            }

            // Fallback: cualquier Camera dentro del árbol de este root
            Camera anyCam = rootObj.GetComponentInChildren<Camera>(true);
            if (anyCam != null) return anyCam;
        }

        return null;
    }

    public void EndGame()
    {
        RacingGameUIManager.Instance.EndGame(ChangeScene);
        gameStarted = false;
        isPaused = true;
    }

    public void ExitGame()
    {
        RacingGameUIManager.Instance.ExitGame(ChangeScene);
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

    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void OnStreamButtonClicked()
    {
        streaming = (!streaming);
        StreamManager.Instance.FlagWebSocketServer();
    }

    public void OnADBButtonClicked()
    {

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
}
