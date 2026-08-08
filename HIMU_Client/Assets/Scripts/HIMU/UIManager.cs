using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private bool changingScene = false;

    [SerializeField] private TextMeshProUGUI statusMessageText;
    [SerializeField] private Image fadeOutImage;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject sessionPrefab;

    // Efecto de texto
    [SerializeField] private float timeToFadeText = 1f;
    [SerializeField] private float timeToFadeScene = 1.5f;
    private float timer = 0.0f;
    private bool fadingText = false;
    private bool fadingScene = false;
    private Color auxColor = Color.black;

    private List<GameObject> sessionList = new List<GameObject>();
    private Dictionary<String, GameObject> sessions = new Dictionary<string, GameObject>(); // mapa de sesiones (IP, Objeto en scrollView)
    private int numSessions = 0;

    public bool IsChangingScene()
    {
        return changingScene;
    }

    private void AssignSessionNames()
    {
        for (int i = 0; i < sessionList.Count; i++)
        {
            sessionList[i].name = "Session" + (i + 1).ToString();
        }
    }

    public void AddNewSessionUI(ConnectionData data)
    {
        if (sessions.ContainsKey(data.ipAddress)) return;

        numSessions++;
        GameObject newSession = Instantiate(sessionPrefab, contentParent);
        SessionInfoComponent infoComponent = newSession.GetComponent<SessionInfoComponent>();
        infoComponent.SetData(data);

        newSession.name = "Session" + numSessions.ToString();
        sessionList.Add(newSession);
        sessions.Add(data.ipAddress, newSession);
    }

    public void ConnectionAttemp()
    {
        statusMessageText.text = "Connecting...";
        statusMessageText.alpha = 1;
        statusMessageText.color = Color.yellow;
    }

    public void ConnectionSuccessful()
    {
        statusMessageText.text = "Connection was successful...";
        statusMessageText.alpha = 1;
        statusMessageText.color = Color.green;
        fadingScene = true;
        changingScene = true;
        fadeOutImage.raycastTarget = true;
    }

    public void ConnectionFailed(string ip)
    {
        statusMessageText.text = "Couldnt connect to session";
        statusMessageText.alpha = 1;
        statusMessageText.color = Color.red;
        fadingText = true;
        
        if (ip != "")
        {
            sessionList.Remove(sessions[ip]);
            DestroyImmediate(sessions[ip]);
            sessions.Remove(ip);
            numSessions--;
            AssignSessionNames();
        }
    }

    public void ChangeScene()
    {
        SceneManager.LoadScene("GameScene");
    }

    private void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (fadingScene)
        {
            timer += Time.deltaTime;
            auxColor = fadeOutImage.color;
            auxColor.a = timer / timeToFadeScene;
            fadeOutImage.color = auxColor;
            if (timer >= timeToFadeScene)
            {
                fadingScene = false;
                timer = 0.0f;
                auxColor = Color.black;
                auxColor.a = 0;
                fadeOutImage.color = auxColor;
                ChangeScene();
            }
        }

        if (fadingText)
        {
            timer += Time.deltaTime;
            auxColor = statusMessageText.color;
            auxColor.a = 1 - timer / timeToFadeText;
            statusMessageText.color = auxColor;
            if (timer >= timeToFadeText)
            {
                fadingText = false;
                timer = 0.0f;
                auxColor = Color.black;
                statusMessageText.text = string.Empty;
            }
        }
    }
}
