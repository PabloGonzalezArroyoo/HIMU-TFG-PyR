using System.Collections;
using System.Net;
using TMPro;
using UnityEngine;

public class SessionInfoComponent : MonoBehaviour
{
    #region Variables
    /// <summary>
    /// Reference to the text element used to show the name of the session
    /// </summary>
    [SerializeField] private TextMeshProUGUI nameText;
    /// <summary>
    /// Reference to the text element used to display info about the session
    /// </summary>
    [SerializeField] private TextMeshProUGUI infoText;

    /// <summary>
    /// IP of the session found
    /// </summary>
    public string IP = "127.0.0.1";
    /// <summary>
    /// Port to connect of the session found
    /// </summary>
    public int port = 7777;
    /// <summary>
    /// Reference to the Connection data received from the device of the session
    /// </summary>
    private ConnectionData connectionData;
    /// <summary>
    /// Name of the session
    /// </summary>
    public string sessionName = "";
    /// <summary>
    /// Info about the session
    /// </summary>
    public string sessionInfo = "";
    #endregion

    #region Methods
    /// <summary>
    /// Method used to establish ConnectionData on this component
    /// </summary>
    /// <param name="data"></param>
    public void SetData(ConnectionData data)
    {
        IP = data.ipAddress;
        port = data.port;
        connectionData = data;
        sessionName = data.sessionName;
        sessionInfo = data.sessionID.ToString();
    }

    /// <summary>
    /// Coroutine executed to update UI state after a connection attemp
    /// </summary>
    /// <returns></returns>
    private IEnumerator UpdateUIPostConnectionAttemp()
    {
        while (ConnectionManager.Instance.currentState == ClientConnectionState.Connecting)
            yield return null;

        if (ConnectionManager.Instance.connected)
            UIManager.Instance.ConnectionSuccessful();
        else
            UIManager.Instance.ConnectionFailed(IP);
    }

    /// <summary>
    /// Method executed when we press this session button. It only tries to establish a connection with the device we recived the session info from if we are not already changing the scene
    /// </summary>
    public void SelectSession()
    {
        if (UIManager.Instance.IsChangingScene()) return;
        ConnectionManager.Instance.ConnectViaTCP(connectionData);
        StartCoroutine(UpdateUIPostConnectionAttemp());
    }
    #endregion

    void Start()
    {
        nameText.text = sessionName;
        infoText.text = sessionInfo;
    }
}