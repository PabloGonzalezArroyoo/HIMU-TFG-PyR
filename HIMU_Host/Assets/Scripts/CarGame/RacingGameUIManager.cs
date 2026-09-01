using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements.Experimental;

public class RacingGameUIManager : MonoBehaviour
{
    public static RacingGameUIManager Instance { get; private set; }

    [SerializeField]
    private GameObject initialStateUI;
    [SerializeField]
    private TextMeshProUGUI counterText;
    [SerializeField]
    private TextMeshProUGUI streamingText;
    [SerializeField]
    private TextMeshProUGUI timerText;
    [SerializeField]
    private GameObject disconnectionText;
    [SerializeField]
    private GameObject pauseMenu;
    [SerializeField]
    private FadeOutComponent fadeOutImage;

    private float startGameCounter = 6.0f;
    private float raceCounter = 0f;

    private bool phoneConnected = true;

    private bool isChangingScene = false;

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
        if (!isChangingScene)
        {
            isChangingScene = true;
            fadeOutImage.StartFading("RacingGame_EndScene", RacingGameManager.Instance.ChangeScene);
        }
    }

    public void ShowDisconnectionText()
    {
        disconnectionText.SetActive(true);
    }

    public void ExitGame()
    {
        if (!isChangingScene) {
            isChangingScene = true;
            fadeOutImage.StartFading("RacingGame_MenuScene", RacingGameManager.Instance.ChangeScene);
            UnityMainThreadDispatcher.Instance().Enqueue(() => Destroy(StreamManager.Instance.gameObject));
        }
    }

    public void StreamSwitched()
    {
        RacingGameManager.Instance.OnStreamButtonClicked();
        streamingText.gameObject.SetActive(RacingGameManager.Instance.streaming);
        streamingText.text = "STREAMING ON " + StreamManager.Instance.GetNodeServerData() + "\nSession: " + StreamManager.Instance.GetSessionID().ToString();
    }

    private IEnumerator CheckControllerConnected()
    {
        while (phoneConnected && !isChangingScene)
        {
            phoneConnected = StreamManager.Instance.GetADBClients().Count > 0;
            yield return null;
        }

        if (!phoneConnected)
        {
            RacingGameManager.Instance.gameStarted = false;
            RacingGameManager.Instance.isPaused = false;
            ShowDisconnectionText();
            if (isChangingScene) fadeOutImage.CancelFade();
            ExitGame();
        }
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
        streamingText.gameObject.SetActive(RacingGameManager.Instance.streaming);
        pauseMenu.SetActive(false);
        StartCoroutine(CheckControllerConnected());
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

        if (RacingGameManager.Instance.gameStarted && !RacingGameManager.Instance.isPaused && !isChangingScene) {
            raceCounter += Time.deltaTime;
            int raceCounterInt = (int) raceCounter;
            timerText.text = Math.Min(raceCounterInt, 999).ToString();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            RacingGameManager.Instance.SetScore(Math.Min((int)raceCounter, 999));
        }
    }
}
