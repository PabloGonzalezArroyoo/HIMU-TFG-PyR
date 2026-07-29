using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RacingConnectionsUIManager : MonoBehaviour
{
    public static RacingConnectionsUIManager Instance { get; private set; }

    [SerializeField]
    private FadeOutComponent fadeOutImage;
    [SerializeField]
    private TextMeshProUGUI streamText;

    public void StartGame(Action<string> callback)
    {
        fadeOutImage.StartFading();
        fadeOutImage.SetCallback(callback);
    }

    public void UpdateADBClientsText(bool conected)
    {

    }

    public void StreamSwitched(bool active)
    {
        streamText.gameObject.SetActive(active);
        if (active)
        {
            streamText.text = "STREAMING ON " + StreamManager.Instance.GetServerData();
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
}
