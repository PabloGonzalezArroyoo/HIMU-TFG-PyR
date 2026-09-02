using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    #region Variables
    /// <summary>
    /// Instance of UIManager (singleton)
    /// </summary>
    public static UIManager Instance { get; private set; }

    /// <summary>
    /// Variable that controls whether we are changing scene or not
    /// </summary>
    private bool changingScene = false;

    /// <summary>
    /// Reference to the text element used to display status
    /// </summary>
    [SerializeField] private TextMeshProUGUI statusMessageText;
    /// <summary>
    /// Reference to the transform of the content panel element in the UI
    /// </summary>
    [SerializeField] private Transform contentParent;
    /// <summary>
    /// Reference to prefab object that represents a session
    /// </summary>
    [SerializeField] private GameObject sessionPrefab;
    /// <summary>
    /// Reference to the button element used to start an adb connection attemp
    /// </summary>
    [SerializeField] private GameObject adbButton;
    /// <summary>
    /// Reference to the UI element used in fade out
    /// </summary>
    [SerializeField] private RawImage fadeOutImage;

    /// <summary>
    /// Time specified to fade texts
    /// </summary>
    [SerializeField] private float timeToFadeText = 1f;
    /// <summary>
    /// Timer used to control fade of texts
    /// </summary>
    private float timer = 0.0f;
    /// <summary>
    /// Variable that inndicates if there is a text fading
    /// </summary>
    private bool fadingText = false;
    /// <summary>
    /// Color auxiliar variable used to change text opacity during fade out effect
    /// </summary>
    private Color auxColor = Color.black;

    /// <summary>
    /// Structure that contains references to the sessions objects in UI 
    /// </summary>
    private List<GameObject> sessionList = new List<GameObject>();

    /// <summary>
    /// Structure that offers access to the session objects in UI by the IP of the session
    /// </summary>
    private Dictionary<String, GameObject> sessions = new Dictionary<string, GameObject>();

    /// <summary>
    /// Counter of current sessions displayed
    /// </summary>
    private int numSessions = 0;
    #endregion

    #region Connection methods
    /// <summary>
    /// Method that indicates in UI that we are currently trying to establish a connection
    /// </summary>
    public void ConnectionAttemp()
    {
        statusMessageText.text = "Connecting...";
        statusMessageText.alpha = 1;
        statusMessageText.color = Color.yellow;
    }

    /// <summary>
    /// Method that updates the UI accordingly when we establish a connection
    /// </summary>
    public void ConnectionSuccessful()
    {
        if (changingScene) return;
        statusMessageText.text = "Connection was successful...";
        statusMessageText.alpha = 1;
        statusMessageText.color = Color.green;
        changingScene = true;
        AppManager.Instance.StartFading(fadeOutImage, "GameScene");
    }

    /// <summary>
    /// Method that updates the UI accordingly when connection attemp failed or we lost comunication abruptly
    /// </summary>
    /// <param name="ip"></param>
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
    #endregion

    #region Session methods
    /// <summary>
    /// Method that updates sessions gameobject name when one is eliminated or added
    /// </summary>
    private void AssignSessionNames()
    {
        for (int i = 0; i < sessionList.Count; i++)
        {
            sessionList[i].name = "Session" + (i + 1).ToString();
        }
    }

    /// <summary>
    /// Method that adds a new session in content pannel
    /// </summary>
    /// <param name="data"></param>
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
    #endregion

    #region Buttons methods
    /// <summary>
    /// Method that activates CONNECT VIA USB button
    /// </summary>
    /// <param name="active"></param>
    public void UpdateADBButton(bool active)
    {
        adbButton.SetActive(active);
    }

    /// <summary>
    /// Method executed when Exit button of ConnectionsScene is pressed
    /// </summary>
    public void ExitConnectionScene()
    {
        if (changingScene) return;
        changingScene = true;
        AppManager.Instance.StartFading(fadeOutImage, "MainMenuScene");
    }
    #endregion

    /// <summary>
    /// Method that informs whether we are currently in a scene transition or not
    /// </summary>
    /// <returns></returns>
    public bool IsChangingScene()
    {
        return changingScene;
    }

    #region MonoBehaviour
    private void Awake()
    {
        if (Instance)
        {
            try { Destroy(Instance.gameObject); }
            catch { Debug.Log("No se pudo borrar el objeto del singleton"); }
        }
        Instance = this;
    }

    void Update()
    {
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
    #endregion
}
