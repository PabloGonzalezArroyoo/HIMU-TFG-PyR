using TMPro;
using UnityEngine;

public class RacingEndUIManager : MonoBehaviour
{
    public static RacingEndUIManager Instance { get; private set; }

    [SerializeField]
    private FadeOutComponent fadeOutImage;
    [SerializeField]
    private TextMeshProUGUI recordText;

    public void GoToMenu()
    {
        fadeOutImage.StartFading();
        fadeOutImage.SetScene("RacingGame_MenuScene");
        fadeOutImage.SetCallback(RacingGameManager.Instance.ChangeScene);
        Destroy(StreamManager.Instance.gameObject);
    }

    public void RepeatGame()
    {
        fadeOutImage.StartFading();
        fadeOutImage.SetScene("RacingGame_ConnectionsScene");
        fadeOutImage.SetCallback(RacingGameManager.Instance.ChangeScene);
        Destroy(StreamManager.Instance.gameObject);
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

    private void Start()
    {
        recordText.text += (RacingGameManager.Instance.GetScore() + " seconds");
    }
}
