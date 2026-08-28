using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShooterConnectionsUIManager : MonoBehaviour
{
    [SerializeField]
    private GameObject wifiButton;
    [SerializeField] private FadeOutComponent fadeOutImage;

    [SerializeField]
    private List<GameObject> wifiConnections;

    [SerializeField]
    private GameObject streamText;

    [SerializeField]
    private GameObject playButton;

    bool showPlay;

    public void WiFiConnButton()
    {
        Button bttComp = wifiButton.GetComponent<Button>();
        bttComp.enabled = !bttComp.isActiveAndEnabled;

        TextMeshProUGUI bttText = wifiButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        bttText.text = "Searching...";
    }

    public void StreamConnButton()
    {
        streamText.SetActive(true);
        TextMeshProUGUI strText = streamText.GetComponent<TextMeshProUGUI>();
        strText.text = $"Streaming on {NetworkUtils.GetIP()}:3000\nSession: {StreamManager.Instance.sessionID}";

        if (FrameCaptureFeature.Instance != null)
        {
            FrameCaptureComponent fccomp = StreamManager.Instance.gameObject.GetComponent<FrameCaptureComponent>();
            fccomp.enabled = true;
            FrameCaptureFeature.Instance.SetCaptureEnabled(true);
            FrameCaptureFeature.Instance.SetSourceCamera(Camera.main);
            ChangeStreamTextures();
        }
        else
        {
            Debug.LogError("[ShooterConnectionManager] Can't enable FCF because there is no feature assigned in the PC_Renderer asset.");
        }
    }

    public void ChangeStreamTextures()
    {
        List<ClientData> browserClients = StreamManager.Instance.GetBrowserClients();
        foreach (ClientData client in browserClients)
        {
            client.himuClient.ChangeTexture(FrameCaptureFeature.Instance.GetFrame());
        }
    }

    public void GoToGame()
    {
        fadeOutImage.StartFading("ShooterScene", LoadScene);
        SceneManager.sceneLoaded += ShooterGameManager.Instance.OnSceneChanged;
    }

    public void ExitGame()
    {
        fadeOutImage.StartFading("ShooterMainMenu", LoadScene);
    }

    private void LoadScene(string scene)
    {
        SceneManager.LoadScene(scene);
    }

    void Start()
    {
        showPlay = false;
    }

    // Update is called once per frame
    void Update()
    {
        List<ClientData> wifiClients = StreamManager.Instance.GetTCPClients();

        showPlay = wifiClients.Count > 0;
        playButton.SetActive(showPlay);

        for (int i = 0; i < wifiClients.Count; i++)
            wifiConnections[i].GetComponent<TextMeshProUGUI>().text = "P" + (i + 1) + "\n" + wifiClients[i].clientID.Substring(0, 3);

    }
}
