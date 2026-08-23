using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

struct PlayerInfo
{
    public int playerN;
    public Scene controlScene;

    public PlayerInfo(int pN, Scene cS)
    {
        playerN = pN;
        controlScene = cS;
    }
}

public class ShooterGameManager : MonoBehaviour
{
    #region Variables

    public static ShooterGameManager Instance { get; private set; }

    [SerializeField] private List<Transform> spawnPositions;

    private Dictionary<string, PlayerInfo> players = new Dictionary<string, PlayerInfo>();

    [SerializeField] private Camera spectatorCamera;

    #endregion

    #region OnGameStart

    private void OrganizePeers()
    {
        List<ClientData> allPeers = StreamManager.Instance.GetClients();

        // Filter the player peers from the browser peers
        List<ClientData> playerPeers = allPeers.Where(p => p.type != ClientConnectionType.WEB_SOCKET).ToList();

        if (playerPeers.Count > spawnPositions.Count)
        {
            Debug.LogError("[ShooterGameManager] " + allPeers.Count + " connected peers but there are ony " +
                            spawnPositions.Count + " spawnpositions. Aborting OrganizePeers...");
            return;
        }

        for (int i = 0; i < playerPeers.Count; i++)
        {
            string clientID = playerPeers[i].clientID;
            Transform player = spawnPositions[i].GetChild(0);
            player.GetComponent<PlayerLifeComponent>().SetClientID(clientID);
            player.GetChild(1).GetChild(0).GetComponent<TextMeshProUGUI>().text = clientID;
            player.GetChild(1).GetChild(1).GetComponent<TextMeshProUGUI>().text = clientID;
            
            players[clientID] = new PlayerInfo(i, default);
        }

        RegisterSpectatorsSource();

        StartCoroutine(LoadRemoteControlScenes(playerPeers));
    }

    private void RegisterSpectatorsSource()
    {
        if (FrameCaptureFeature.Instance == null)
        {
            Debug.LogError("[ShooterGameManager] FrameCaptureFeature not present in the active URP renderer. Spectating will not work.");
            return;
        }

        if (spectatorCamera == null)
        {
            Debug.LogError("[ShooterGameManager] spectatorCamera is not assigned. Aborting spectators source registration...");
            return;
        }

        FrameCaptureFeature.Instance.SetSourceCamera(spectatorCamera);
    }

    private IEnumerator LoadRemoteControlScenes(List<ClientData> peers)
    {
        foreach (var peer in peers)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync("ShooterRemoteControlScene", LoadSceneMode.Additive);
            yield return op;

            Scene loaded = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

            PlayerInfo info = players[peer.clientID];
            info.controlScene = loaded;
            players[peer.clientID] = info;
        }
    }

    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void LoadRemoteControlScene(Scene current, Scene next)
    {
        if (next.name != "ShooterScene") return;
        OrganizePeers();
    }

    public void OnSceneChanged(Scene loadedScene, LoadSceneMode mode)
    {
        // Cuando se carga la escena de mando -> seteamos la camara del cliente adb
        if (mode == LoadSceneMode.Additive)
        {
            //Camera backgroundCamera = FindCameraInScene(loadedScene, "RemoteControl_Camera");
            //backgroundCamera.targetTexture = controlTexture;
            //AssignADBTexture();
            //Debug.Log("Escena de mando cargada");
        }

        // Cuando se carga la escena de juego -> seteamos la camara de los clientes WebSocket
        if (loadedScene.name.Contains("Main"))
        {
            //Camera mainCamera = FindCameraInScene(loadedScene, "StreamCamera");
            //mainCamera.targetTexture = gameTexture;
            //ChangeStreamTextures();
            //Debug.Log("Escena de juego cargada");
        }

        // Cuando se carga la escena de conexiones
        if (loadedScene.name.Contains("Connections"))
        {
            //StreamManager.Instance.SetADBClientCallback(CreateADBClient);
            //StreamManager.Instance.SetBrowserClientCallback(CreateBrowserClient);
            //Camera streamCamera = new GameObject().AddComponent<Camera>();
            //streamCamera.gameObject.transform.position = FindCameraInScene(loadedScene, "Main Camera").gameObject.transform.position;
            //streamCamera.targetTexture = connectionsTexture;
            //StreamManager.Instance.SetADBTextureCallback(TextureOnConnections);
            //StreamManager.Instance.SetBrowserTextureCallback(TextureOnConnections);
        }
    }

    #endregion

    #region Game Methods

    public void PlayerEliminated(string player)
    {
        players.Remove(player);

        if (players.Count == 1)
            ShooterUIManager.Instance.SetVictoryUIState(players.First().Value.playerN);
    }

    #endregion

    #region Monobehaviour

    private void Awake()
    {
        if (Instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;

        SceneManager.activeSceneChanged += LoadRemoteControlScene;
        SceneManager.sceneLoaded += OnSceneChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.activeSceneChanged -= LoadRemoteControlScene;
            SceneManager.sceneLoaded -= OnSceneChanged;
            FrameCaptureFeature.Instance?.SetSourceCamera(null);
        }
    }

    #endregion
}
