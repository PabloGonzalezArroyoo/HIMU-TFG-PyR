using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShooterUIManager : MonoBehaviour
{
    public static ShooterUIManager Instance { get; private set; }
    
    [SerializeField] private GameObject victoryCanvas;

    [SerializeField] private FadeOutComponent fadeOutImage;

    public void SetVictoryUIState(int player)
    {
        victoryCanvas.SetActive(true);
        TextMeshProUGUI shadow = victoryCanvas.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI text = victoryCanvas.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        shadow.text = "PLAYER " + (player + 1) + " WINS!!!";
        text.text = "PLAYER " + (player + 1) + " WINS!!!";
    }

    public void PlayAgain()
    {
        victoryCanvas.SetActive(false);
        ShooterGameManager.Instance.ResetGame();
    }

    public void ExitGame()
    {
        fadeOutImage.StartFading("ShooterMainMenu", ShooterGameManager.Instance.ChangeScene);
        SceneManager.sceneLoaded -= ShooterGameManager.Instance.OnSceneChanged;
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
