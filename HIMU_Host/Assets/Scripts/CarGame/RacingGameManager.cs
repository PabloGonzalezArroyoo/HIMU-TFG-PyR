using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RacingGameManager : MonoBehaviour
{
    public static RacingGameManager Instance { get; private set; }

    private RenderTexture connectionsTexture;
    private RenderTexture gameTexture;
    private RenderTexture controlTexture;

    private PointerComponent pausePointer;

    public bool gameStarted = false;
    public bool isPaused = false;
    public bool streaming = false;

    private string clientID = "";

    private int recordTime = 0;

    private HashSet<int> buttonIDs = new HashSet<int>();

    #region Getters&Setters
    public int GetScore()
    {
        return recordTime;
    }

    public void SetScore(int record)
    {
        recordTime = record;
    }

    public string GetPlayerID()
    {
        return clientID;
    }

    public void SetPlayerID(string id)
    {
        clientID = id;
    }

    public PointerComponent GetPauseMenuPointer()
    {
        return pausePointer;
    }

    public void SetPauseMenuPointer(PointerComponent pointer)
    {
        pausePointer = pointer;
    }
    #endregion

    #region Textures
    private void ChangeStreamTextures()
    {
        List<ClientData> browserClients = StreamManager.Instance.GetBrowserClients();
        foreach (ClientData client in browserClients)
        {
            client.himuClient.ChangeTexture(FrameCaptureFeature.Instance.GetFrame());
        }
    }

    private void AssignADBTexture()
    {
        List<ClientData> adbClients = StreamManager.Instance.GetADBClients();
        foreach (ClientData client in adbClients)
        {
            client.himuClient.ChangeTexture(controlTexture);
        }
    }

    private void PrepareTextures()
    {
        connectionsTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.BGRA32);
        connectionsTexture.enableRandomWrite = true;
        connectionsTexture.useMipMap = false;
        connectionsTexture.antiAliasing = 1;
        connectionsTexture.Create();

        gameTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.BGRA32);
        gameTexture.enableRandomWrite = true;
        gameTexture.useMipMap = false;
        gameTexture.antiAliasing = 1;
        gameTexture.Create();

        controlTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.BGRA32);
        controlTexture.enableRandomWrite = true;
        controlTexture.useMipMap = false;
        controlTexture.antiAliasing = 1;
        controlTexture.Create();
    }
    #endregion

    #region OnGameStart
    private IEnumerator LoadBackgroundSceneAsync()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync("RacingGame_RemoteControlScene", LoadSceneMode.Additive);

        while (!op.isDone)
            yield return null;

        Scene loadedScene = SceneManager.GetSceneByName("RacingGame_RemoteControlScene");
    }

    public void OnGameStarted(Scene current, Scene next)
    {
        if (next.name != "RacingGame_MainScene") return;
        StartCoroutine(LoadBackgroundSceneAsync());
    }


    public void OnSceneChanged(Scene loadedScene, LoadSceneMode mode)
    {
        // Cuando se carga la escena de conexiones
        if (loadedScene.name.Contains("Connections"))
        {
            StreamManager.Instance.SetADBClientCallback(CreateADBClient);
            StreamManager.Instance.SetBrowserClientCallback(CreateBrowserClient);
            Camera streamCamera = new GameObject().AddComponent<Camera>();
            streamCamera.gameObject.transform.position = FindCameraInScene(loadedScene, "Main Camera").gameObject.transform.position;
            streamCamera.targetTexture = connectionsTexture;
            StreamManager.Instance.SetADBTextureCallback(TextureOnConnections);
            StreamManager.Instance.SetBrowserTextureCallback(TextureOnConnections);
        }

        // Cuando se carga la escena de juego -> seteamos la camara de los clientes WebSocket
        if (loadedScene.name.Contains("Main") && FrameCaptureFeature.Instance != null)
        {
            FrameCaptureFeature.Instance.SetCaptureEnabled(true);
            FrameCaptureComponent fccomp = gameObject.AddComponent<FrameCaptureComponent>();
            FrameCaptureFeature.Instance.SetSourceCamera(FindCameraInScene(loadedScene, "PlayerCamera"));
            ChangeStreamTextures();
            Debug.Log("Se aplican la textura del frame capture");
        }

        // Cuando se carga la escena de mando -> seteamos la camara del cliente adb
        if (mode == LoadSceneMode.Additive)
        {
            Camera backgroundCamera = FindCameraInScene(loadedScene, "RemoteControl_Camera");
            backgroundCamera.targetTexture = controlTexture;
            AssignADBTexture();
        }
    }

    private Camera FindCameraInScene(UnityEngine.SceneManagement.Scene scene, string cameraName)
    {
        // Recorremos los objetos raíz de la escena buscando la cámara
        foreach (GameObject rootObj in scene.GetRootGameObjects())
        {
            // Si se especificó un nombre concreto, priorizamos búsqueda exacta
            if (!string.IsNullOrEmpty(cameraName))
            {
                if (rootObj.name == cameraName)
                {
                    Camera cam = rootObj.GetComponent<Camera>();
                    if (cam != null) return cam;
                }

                Transform found = rootObj.transform.Find(cameraName);
                if (found != null)
                {
                    Camera cam = found.GetComponent<Camera>();
                    if (cam != null) return cam;
                }
            }

            // Fallback: cualquier Camera dentro del árbol de este root
            Camera anyCam = rootObj.GetComponentInChildren<Camera>(true);
            if (anyCam != null) return anyCam;
        }

        return null;
    }

    public int CreateButtonID()
    {
        int id = 0;
        do { id = UnityEngine.Random.Range(10, 100); }
        while (buttonIDs.Contains(id));

        return id;
    }
    #endregion

    #region Create Clients & textures
    public GameObject CreateBrowserClient(ClientData clientData)
    {
        GameObject newClient = new GameObject();
        newClient.transform.position = Vector3.zero;
        return newClient;
    }

    public GameObject CreateADBClient(ClientData clientData)
    {
        GameObject client = new GameObject();
        return client;
    }

    public RenderTexture TextureOnConnections()
    {
        return connectionsTexture;
    }
    #endregion

    #region OnClick Methods
    public void EndGame()
    {
        gameStarted = false;
        isPaused = false;
        RacingGameUIManager.Instance.EndGame();
        if (streaming) StreamManager.Instance.FlagWebSocketServer();
        StreamManager.Instance.FlagADBConnection();
    }

    public void PauseGame()
    {
        isPaused = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
    }

    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void OnStreamButtonClicked()
    {
        streaming = (!streaming);
        StreamManager.Instance.FlagWebSocketServer();
    }
    #endregion

    private void Awake()
    {
        if (Instance)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        PrepareTextures();

        SceneManager.activeSceneChanged += Instance.OnGameStarted;
        SceneManager.sceneLoaded += Instance.OnSceneChanged;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.activeSceneChanged -= RacingGameManager.Instance.OnGameStarted;
        SceneManager.sceneLoaded -= RacingGameManager.Instance.OnSceneChanged;
    }
}
