using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShooterUIManager : MonoBehaviour
{
    public static ShooterUIManager Instance { get; private set; }
    
    [SerializeField] private GameObject victoryCanvas;

    public void SetVictoryUIState(int player)
    {
        victoryCanvas.SetActive(true);
        TextMeshProUGUI shadow = victoryCanvas.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI text = victoryCanvas.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        shadow.text = "PLAYER " + player + " WINS!!!";
        text.text = "PLAYER " + player + " WINS!!!";
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
}
