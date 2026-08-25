using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShooterMenuUIManager : MonoBehaviour
{
    public static ShooterMenuUIManager Instance { get; private set; }
    
    [SerializeField] private FadeOutComponent fadeOutImage;

    public void GoToConnections()
    {
        fadeOutImage.SetCallback(LoadScene);
        fadeOutImage.StartFading();
    }

    private void LoadScene(string scene)
    {
        SceneManager.LoadScene(scene);
    }

    public void ExitGame()
    {
        Application.Quit();
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
