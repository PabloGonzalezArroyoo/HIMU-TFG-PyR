using TMPro;
using UnityEngine;

public class ShooterMenuUIManager : MonoBehaviour
{
    public static ShooterMenuUIManager Instance { get; private set; }
    
    [SerializeField] private FadeOutComponent fadeOutImage;

    public void GoToConnections()
    {
        fadeOutImage.StartFading();
        fadeOutImage.SetCallback(ShooterGameManager.Instance.ChangeScene);
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
}
