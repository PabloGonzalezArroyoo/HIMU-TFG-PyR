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
    private GameObject playButton;

    private bool searchingADB = false;
    public bool deviceFound = false;
    private bool fading = false;
    [SerializeField]
    private float searchLimitTime = 5f;
    private float timer = 0f;

    public void SearchADBClients()
    {
        if (searchingADB) return;

        adbStatusText.text = "SEARCHING";
        adbStatusText.color = Color.yellow;
        searchingADB = true;
        StartCoroutine(SearchForDevices());
    }

    public void StreamSwitched()
    {
        RacingGameManager.Instance.OnStreamButtonClicked();
        streamText.gameObject.SetActive(RacingGameManager.Instance.streaming);
        streamText.text = "STREAMING ON " + StreamManager.Instance.GetServerData();
    }

    public void StartGame()
    {
        fading = true;
        fadeOutImage.StartFading();
        fadeOutImage.SetCallback(RacingGameManager.Instance.ChangeScene);
    }

    private IEnumerator SearchForDevices()
    {
        StreamManager.Instance.FlagADBConnection();
        while (!deviceFound && timer < searchLimitTime)
        {
            timer += Time.deltaTime;
            deviceFound = StreamManager.Instance.GetADBClients().Count > 0;
            yield return null;
        }

        timer = 0f;
        searchingADB = false;
        if (!deviceFound)
        {
            adbStatusText.text = "PHONE MISSING";
            adbStatusText.color = Color.red;
            StreamManager.Instance.FlagADBConnection();
        }
        else
        {
            playButton.SetActive(true);
            adbStatusText.text = "PHONE FOUND";
            adbStatusText.color = Color.green;
            //StartCoroutine(CheckADBClient());
        }
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
            StreamManager.Instance.FlagADBConnection();
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
