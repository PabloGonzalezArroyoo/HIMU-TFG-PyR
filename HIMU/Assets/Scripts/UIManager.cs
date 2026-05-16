using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField]
    private GameObject connectionButton;
    [SerializeField]
    private TextMeshProUGUI connectionText;

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
        connectionButton.SetActive(false);
        connectionText.gameObject.SetActive(true);
        connectionText.text += ip;
    }
}
