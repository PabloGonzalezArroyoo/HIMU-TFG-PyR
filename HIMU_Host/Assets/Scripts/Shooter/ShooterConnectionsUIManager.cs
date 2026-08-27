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

    public void WiFiConnButton()
    {
        Button bttComp = wifiButton.GetComponent<Button>();
        bttComp.enabled = !bttComp.isActiveAndEnabled;

        TextMeshProUGUI bttText = wifiButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        bttText.text = "Searching...";
    }

    public void GoToGame()
    {
        fadeOutImage.SetCallback(LoadScene);
        fadeOutImage.StartFading();
    }

    public void ExitGame()
    {
        fadeOutImage.SetScene("ShooterMainMenu");
        fadeOutImage.SetCallback(LoadScene);
        fadeOutImage.StartFading();
    }

    private void LoadScene(string scene)
    {
        SceneManager.LoadScene(scene);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        List<ClientData> wifiClients = StreamManager.Instance.GetTCPClients();
        for (int i = 0; i < wifiClients.Count; i++)
        {
            wifiConnections[i].GetComponent<TextMeshProUGUI>().text = "P" + (i + 1) + "\n" + wifiClients[i].clientID.Substring(0, 3);
        }
    }
}
