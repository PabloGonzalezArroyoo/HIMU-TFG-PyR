using NUnit.Framework;
using System;
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts;

public class ConnectionUIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statusMessageText;
    [SerializeField] private Transform contentParent;
    [SerializeField] private GameObject sessionPrefab;

    // Efecto de texto
    [SerializeField] private float timeToFadeText = 1.5f;
    private float timer = 0.0f;
    private bool fadingText = false;

    private List<GameObject> sessionList = new List<GameObject>();
    private int numSessions = 10;
    private bool changingScene = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
        newSession.name = "Session" + numSessions.ToString();
        sessionList.Add(newSession);

        SessionInfoComponent infoComponent = newSession.GetComponent<SessionInfoComponent>();
        infoComponent.SetData(data, this);
    }

    public void ConnectionSuccessful()
    {
        // Mostrar texto de conexion, hacer fade out y cambiar de escena
        ChangeScene();
    }

    public void ConnectionFailed()
    {
        // Mostrar texto fallido y hacer fade de ese texto
        // Eliminar esa opcion del scroll view?
    }

    public void ChangeScene()
    {
        // Cambiar la escena
    }
}
