using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HostUIManager : MonoBehaviour
{
    /// <summary>
    /// Instance of StreamManager (Singleton)
    /// </summary>
    public static HostUIManager Instance { get; private set; }

    [SerializeField] 
    private TextMeshProUGUI streamText;
    [SerializeField] 
    private TextMeshProUGUI tcpText;

    int streamClients = 0;
    int tcpClients = 0;

    public void UpdateStreamClientsText(bool adding)
    {
        if (adding) streamClients++;
        else streamClients--;
        streamText.text = streamClients.ToString() + " Connections";
    }

    public void ResetStreamClientsText()
    {
        streamClients = 0;
        streamText.text = "0 Connections";
    }

    public void UpdateTCPClientsText(bool adding)
    {
        if (adding) tcpClients++;
        else tcpClients--;
        tcpText.text = tcpClients.ToString() + " Connections";
    }

    public void ResetTCPClientsText()
    {
        tcpClients = 0;
        tcpText.text = "0 Connections";
    }

    public void GoToPlayScene()
    {
        SceneManager.LoadScene("MainGameScene");
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
}
