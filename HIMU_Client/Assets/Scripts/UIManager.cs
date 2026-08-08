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

    public void OnConnectionStarted(string ip)
    {
        connectionText.gameObject.SetActive(true);
        connectionText.text += ip;
    }
}
