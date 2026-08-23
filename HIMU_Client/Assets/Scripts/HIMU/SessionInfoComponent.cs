using System.Collections;
using System.Net;
using TMPro;
using UnityEngine;

public class SessionInfoComponent : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI infoText;

    public string IP = "127.0.0.1";
    public int port = 7777;
    private ConnectionData connectionData;
    public string sessionName = "";
    public string sessionInfo = "";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        nameText.text = sessionName;
        infoText.text = sessionInfo;
    }

    public void SetData(ConnectionData data)
    {
        IP = data.ipAddress;
        port = data.port;
        connectionData = data;
        sessionName = data.sessionName;
        sessionInfo = data.sessionID.ToString();
    }

    private IEnumerator UpdateUIPostConnectionAttemp()
    {
        while (ConnectionManager.Instance.currentState == ClientConnectionState.Connecting)
        {
            yield return null;
        }
        if (ConnectionManager.Instance.connected)
        {
            UIManager.Instance.ConnectionSuccessful();
        }
        else
        {
            Debug.Log("Se elemina la sesion de la UI");
            UIManager.Instance.ConnectionFailed(IP);
        }
    }

    public void SelectSession()
    {
        // No queremos intentar mas conexiones si estamos cambiando de escena
        if (UIManager.Instance.IsChangingScene()) return;
        ConnectionManager.Instance.ConnectViaTCP(connectionData);
        StartCoroutine(UpdateUIPostConnectionAttemp());
    }
}