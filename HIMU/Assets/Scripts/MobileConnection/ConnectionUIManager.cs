using NUnit.Framework;
using System;
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ConnectionUIManager : MonoBehaviour
{
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
    private Dictionary<String, GameObject> sessions = new Dictionary<string, GameObject>();
    private int numSessions = 0;
    private bool changingScene = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < 5; i++) {
            AddNewSessionUI(new ConnectionData("192.0.2."+i.ToString(), 7777, ConnectionEvent.HANDSHAKE));
        }
    }

    // Update is called once per frame
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

    public bool IsChangingScene()
    {
        return changingScene;
    }

    private void AssignSessionNames()
    {
        for (int i = 0; i < sessionList.Count; i++) {
            sessionList[i].name = "Session" + (i + 1).ToString();
        }
    }

    public void AddNewSessionUI(ConnectionData data)
    {
        numSessions++;
        GameObject newSession = Instantiate(sessionPrefab, contentParent);
        SessionInfoComponent infoComponent = newSession.GetComponent<SessionInfoComponent>();
        infoComponent.SetData(data, this);

        newSession.name = "Session" + numSessions.ToString();
        sessionList.Add(newSession);
        sessions.Add(data.ipAddress, newSession);
    }

    public void ConnectionSuccessful()
    {
        // Mostrar texto de conexion, hacer fade out y cambiar de escena
        statusMessageText.text = "Connection was successful...";
        fadingScene = true;
        changingScene = true;
    }

    public void ConnectionFailed(string ip)
    {
        // Mostrar texto fallido y hacer fade de ese texto
        statusMessageText.text = "Couldnt connect to session";
        fadingText = true;
        // Eliminar esa opcion del scroll view
        DestroyImmediate(sessions[ip]);
        sessionList.Remove(sessions[ip]);
        sessions.Remove(ip);
        numSessions--;
        AssignSessionNames();
    }

    public void ChangeScene()
    {
        // Cambiar la escena
        SceneManager.LoadScene("GameScene");
    }
}
