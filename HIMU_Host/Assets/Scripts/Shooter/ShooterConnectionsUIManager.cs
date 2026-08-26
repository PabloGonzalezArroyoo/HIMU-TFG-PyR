using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ShooterConnectionsUIManager : MonoBehaviour
{
    [SerializeField]
    private GameObject wifiButton;

    [SerializeField]
    private TextMeshProUGUI wifiConnections;

    private int devConnected;

    public void WiFiConnButton()
    {
        Button bttComp = wifiButton.GetComponent<Button>();
        bttComp.enabled = !bttComp.isActiveAndEnabled;

        TextMeshProUGUI bttText = wifiButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        bttText.text = "Searching ...";
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        devConnected = 0;
        wifiConnections.text = " ";
    }

    // Update is called once per frame
    void Update()
    {
        List<ClientData> wifiClients = StreamManager.Instance.GetTCPClients();
        if (wifiClients.Count > devConnected)
        {
            wifiConnections.text = " ";
            for (int i = 0; i < wifiClients.Count; i++)
                wifiConnections.text += ">P" + i + " ";
            devConnected = wifiClients.Count;
        }
    }
}
