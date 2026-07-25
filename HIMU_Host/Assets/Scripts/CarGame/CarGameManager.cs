using UnityEngine;
using UnityEngine.InputSystem;

public class CarGameManager : MonoBehaviour
{
    public static CarGameManager Instance { get; private set; }

    public bool gameStarted = false;
    public bool isPaused = false;

    public void EndGame()
    {
        CarGameUIManager.Instance.EndGame();
    }

    public void PauseGame()
    {
        isPaused = true;
        CarGameUIManager.Instance.Pause();
    }

    public void ResumeGame()
    {
        isPaused = false;
        CarGameUIManager.Instance.Resume();
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.escapeKey.wasReleasedThisFrame)
        {
            if(isPaused) ResumeGame();
            else PauseGame();
        }
    }
}
