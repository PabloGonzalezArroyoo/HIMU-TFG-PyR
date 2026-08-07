using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Old_UIManager : MonoBehaviour
{
    public static Old_UIManager Instance { get; private set; }

    [SerializeField]
    private TextMeshProUGUI connectionText;
    [SerializeField]
    private StreamManager connectionMannager;
    [SerializeField]
    private GameObject sessionButtons;
    [SerializeField]
    private GameObject controlButtons;

    void Awake()
    {
        if (Instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
    }

    public void StartConnection(int c)
    {
        ClientType cl;
        switch(c)
        {
            case 0: 
                cl = ClientType.WEB_SOCKET;
                Debug.Log("STREAM SELECCIONADO");
                break;
            case 1: 
                cl = ClientType.ADB;
                Debug.Log("PLAYER SELECCIONADO");
                controlButtons.SetActive(true); 
                break;
            default: 
                cl = ClientType.NONE; 
                break;
        }
        connectionMannager.StartBroadcast(cl);  
        sessionButtons.SetActive(false);
    }

    public void OnConnectionStarted(string ip)
    {
        connectionText.gameObject.SetActive(true);
        connectionText.text += ip;
    }
}
