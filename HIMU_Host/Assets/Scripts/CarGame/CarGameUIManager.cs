using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CarGameUIManager : MonoBehaviour
{
    public static CarGameUIManager Instance { get; private set; }

    [SerializeField]
    private GameObject initialStateUI;
    [SerializeField]
    private TextMeshProUGUI counterText;
    [SerializeField]
    private TextMeshProUGUI timerText;
    [SerializeField]
    private GameObject pauseMenu;
    [SerializeField]
    private Image fadeOutImage;

    private float startGameCounter = 6.0f;
    private float raceCounter = 0f;
    [SerializeField]
    private float endGameTime = 2.0f;
    private float endGameCounter = 0f;

    private bool isChangingScene = false;


    private void Awake()
    {
        if (Instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
    }

    public void Pause()
    {
        pauseMenu.SetActive(true);
    }

    public void Resume()
    {
        pauseMenu.SetActive(false);
    }

    public void EndGame()
    {
        isChangingScene = true;
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("RacingGame_EndScene");
    }

    
    void Update()
    {
        if (!CarGameManager.Instance.gameStarted)
        {
            startGameCounter -= Time.deltaTime;
            if (startGameCounter <= 1)
            {
                CarGameManager.Instance.gameStarted = true;
                startGameCounter = 6.0f;
                counterText.text = "5";
                initialStateUI.SetActive(false);
            }
            else
            {
                counterText.text = ((int) startGameCounter).ToString();
            }
        }

        

        if (isChangingScene)
        {
            endGameCounter += Time.deltaTime;
            Color aux = fadeOutImage.color;
            aux.a = endGameCounter / endGameTime;
            fadeOutImage.color = aux;
            if (aux.a >= 1)
            {
                SceneManager.SetActiveScene(SceneManager.GetSceneByName("RacingGame_EndScene"));
            }
        }

        if (CarGameManager.Instance.gameStarted && !isChangingScene) {
            raceCounter += Time.deltaTime;
            int raceCounterInt = (int) raceCounter;
            timerText.text = raceCounterInt > 999 ? 999.ToString() : raceCounterInt.ToString();
        }
    }
}
