using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RacingGameUIManager : MonoBehaviour
{
    public static RacingGameUIManager Instance { get; private set; }

    [SerializeField]
    private GameObject initialStateUI;
    [SerializeField]
    private TextMeshProUGUI counterText;
    [SerializeField]
    private TextMeshProUGUI timerText;
    [SerializeField]
    private GameObject pauseMenu;
    [SerializeField]
    private FadeOutComponent fadeOutImage;

    private float startGameCounter = 6.0f;
    private float raceCounter = 0f;

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

    public void EndGame(Action<string> callback)
    {
        if (!isChangingScene)
        {
            isChangingScene = true;
            fadeOutImage.StartFading();
            fadeOutImage.SetCallback(callback);
        }
    }

    
    void Update()
    {
        if (!RacingGameManager.Instance.gameStarted)
        {
            startGameCounter -= Time.deltaTime;
            if (startGameCounter <= 1)
            {
                RacingGameManager.Instance.gameStarted = true;
                startGameCounter = 6.0f;
                counterText.text = "5";
                initialStateUI.SetActive(false);
            }
            else
            {
                counterText.text = ((int) startGameCounter).ToString();
            }
        }

        if (RacingGameManager.Instance.gameStarted && !isChangingScene) {
            raceCounter += Time.deltaTime;
            int raceCounterInt = (int) raceCounter;
            timerText.text = raceCounterInt > 999 ? 999.ToString() : raceCounterInt.ToString();
        }
    }
}
