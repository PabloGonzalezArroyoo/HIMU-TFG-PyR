using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField]
    private GameObject connectionButton;
    [SerializeField]
    private TextMeshProUGUI connectionText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void Awake()
    {
        if (Instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
    }

    public void OnConnectionButtonClicked()
    {
        ConnectionManager.Instance.StartBroadcast();
    }

    public void OnConnectionStarted(string ip)
    {
        connectionButton.SetActive(false);
        connectionText.gameObject.SetActive(true);
        connectionText.text += ip;
    }
}
