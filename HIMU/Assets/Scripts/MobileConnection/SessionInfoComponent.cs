using Assets.Scripts;
using System.Net;
using TMPro;
using UnityEngine;

public class SessionInfoComponent : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI infoText;

    private ConnectionUIManager uiManager = null;

    public string IP = "127.0.0.1";
    public int port = 7777;
    public string sessionName = "";
    public string sessionInfo = "";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        nameText.text = sessionName;
        infoText.text = sessionInfo;
    }

    public void SetData(ConnectionData data, ConnectionUIManager manager)
    {
        IP = data.ipAddress; 
        port = data.port;
        sessionName = data.name;
        sessionInfo = data.info;
        uiManager = manager;
    }

    public void SelectSession()
    {
        if (uiManager.IsChangingScene()) return;                        // No queremos intentar mas conexiones si estamos cambiando de escena

        if (ComunicationManager.Instance.TryTCPConnection(IP, port))
        {
            uiManager.ConnectionSuccessful();
        }
        else
        {
            Debug.Log("Conexion fallo");
            uiManager.ConnectionFailed(IP);
        }
    }

    private void OnDestroy()
    {
        uiManager = null;
        nameText = null;
        infoText = null;
    }
}
