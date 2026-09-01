using UnityEngine;
using UnityEngine.SceneManagement;

public class RacingMenuUIManager : MonoBehaviour
{
    public static RacingMenuUIManager Instance { get; private set; }

    [SerializeField]
    private FadeOutComponent fadeOutImage;

    public void GoToConnections()
    {
        fadeOutImage.StartFading("RacingGame_ConnectionsScene", RacingGameManager.Instance.ChangeScene);
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
