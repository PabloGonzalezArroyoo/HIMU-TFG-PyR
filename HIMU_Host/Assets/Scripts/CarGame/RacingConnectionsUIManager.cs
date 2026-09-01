using System;
using System.Collections;
using System.Drawing.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RacingConnectionsUIManager : MonoBehaviour
{
    public static RacingConnectionsUIManager Instance { get; private set; }

    [SerializeField]
    private FadeOutComponent fadeOutImage;
    [SerializeField]
    private TextMeshProUGUI adbStatusText;
    [SerializeField]
    private TextMeshProUGUI streamText; 
    [SerializeField]
    private TextMeshProUGUI streamButtonText;
    [SerializeField]
    private GameObject playButton;

    public bool deviceFound = false;
    private bool fading = false;
    [SerializeField]
    private float searchLimitTime = 5f;
    private float timer = 0f;

    public void ExitGame()
    {
        fading = true;
        fadeOutImage.StartFading("RacingGame_MenuScene", RacingGameManager.Instance.ChangeScene);
    }

    public void StartGame()
    {
        fading = true;
        fadeOutImage.StartFading("RacingGame_MainScene", RacingGameManager.Instance.ChangeScene);
    }

    public void StreamSwitched()
    {
        RacingGameManager.Instance.OnStreamButtonClicked();
        streamText.gameObject.SetActive(RacingGameManager.Instance.streaming);
        streamText.text = "STREAMING ON " + StreamManager.Instance.GetNodeServerData() + "\nSession: " + StreamManager.Instance.GetSessionID().ToString();
        streamButtonText.text = RacingGameManager.Instance.streaming ? "STREAM ON" : "STREAM OFF";
    }

    private IEnumerator WaitAfterDisconnection()
    {
        adbStatusText.text = "SEARCHING";
        adbStatusText.color = Color.yellow;

        while (timer <= searchLimitTime)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        timer = 0f;
        StartCoroutine(SearchForDevices());
    }

    private IEnumerator SearchForDevices()
    {
        StreamManager.Instance.FlagADBConnection();
        StreamManager.Instance.FlagADBAcceptConnections(true);
        while (!deviceFound)
        {
            deviceFound = StreamManager.Instance.GetADBClients().Count > 0;
            yield return null;
        }

        playButton.SetActive(true);
        adbStatusText.text = "PHONE FOUND";
        adbStatusText.color = Color.green;
        RacingGameManager.Instance.SetPlayerID(StreamManager.Instance.GetADBClients()[0].clientID);
        StreamManager.Instance.FlagADBAcceptConnections(false);
        StartCoroutine(CheckADBClient());
    }

    private IEnumerator CheckADBClient()
    {
        while (deviceFound && !fading)
        {
            deviceFound = StreamManager.Instance.GetADBClients().Count > 0;
            yield return null;
        }

        if (!deviceFound)
        {
            playButton.SetActive(false);
            adbStatusText.text = "PHONE MISSING";
            adbStatusText.color = Color.red;
            if (fading)
            {
                fading = false;
                fadeOutImage.CancelFade();
            }
            StreamManager.Instance.FlagADBConnection(); // Desactivamos busqueda de dispositivos para notificar al usuario
            StartCoroutine(WaitAfterDisconnection());
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
        StartCoroutine(SearchForDevices());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
