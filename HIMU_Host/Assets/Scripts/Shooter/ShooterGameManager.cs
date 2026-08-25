using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

class PlayerInfo
{
    public int playerN;

    public ShooterPlayerController controller;

    public Scene controlScene;

    // Virtual input device backed by controlScene
    public RemoteControlRig rig;

    public PlayerInfo(int playerN, ShooterPlayerController controller)
    {
        this.playerN = playerN;
        this.controller = controller;
    }
}

public class ShooterGameManager : MonoBehaviour
{

    #region Variables

    public static ShooterGameManager Instance { get; private set; }

    [SerializeField] private List<Transform> spawnPositions;

    [SerializeField] private Camera spectatorCamera; 

    private Dictionary<string, PlayerInfo> players = new Dictionary<string, PlayerInfo>();

    [SerializeField] private string remoteControlSceneName = "ShooterRemoteControlScene";

    /// <summary>
    /// Distance along +X between consecutive copies of the control scene.
    /// </summary>
    [SerializeField] private float controlSceneOffset = 1000f;

    #endregion

    #region OnGameStart

    private void OrganizePeers()
    {
        players.Clear();

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

            PlayerLifeComponent life = player.GetComponent<PlayerLifeComponent>();
            if (life != null) life.SetClientID(clientID);

            ShooterPlayerController controller = player.GetComponent<ShooterPlayerController>();
            if (controller == null)
                Debug.LogError("[ShooterGameManager] Avatar at spawn " + i + " has no ShooterPlayerController.");

            player.GetChild(1).GetChild(0).GetComponent<TextMeshProUGUI>().text = clientID;
            player.GetChild(1).GetChild(1).GetComponent<TextMeshProUGUI>().text = clientID;
            
            players[clientID] = new PlayerInfo(i, controller);
        }

        RegisterSpectatorsSource();

        StartCoroutine(SetUpRemoteControls(playerPeers));
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

    /// <summary>
    /// Loads one copy of the control scene per peer and wires the three ends of the loop: the
    /// client's input feeds the rig, the rig drives that client's avatar, and the control camera
    /// renders into the RenderTexture that client is streaming.
    ///
    /// Loads are serialized (one yield per scene) so that the newly loaded scene is always the
    /// last one in SceneManager's list, which is the only way to obtain the Scene handle of a
    /// copy: several copies of the same asset share name and path, so lookups by name are
    /// ambiguous by construction.
    /// </summary>
    /// <param name="peers">Peers that take part in the match, in spawn order.</param>
    private IEnumerator SetUpRemoteControls(List<ClientData> peers)
    {
        for (int i = 0; i < peers.Count; i++)
        {
            ClientData peer = peers[i];

            AsyncOperation op = SceneManager.LoadSceneAsync(remoteControlSceneName, LoadSceneMode.Additive);
            yield return op;

            Scene loaded = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            if (!loaded.IsValid() || loaded.name != remoteControlSceneName)
            {
                Debug.LogError($"[ShooterGameManager] Could not resolve the control scene loaded for " + peer.clientID + ".");
                continue;
            }

            OffsetScene(loaded, i);

            if (!players.TryGetValue(peer.clientID, out PlayerInfo info))
            {
                Debug.LogError($"[ShooterGameManager] No player registered for " + peer.clientID + ".");
                continue;
            }

            RemoteControlRig rig = FindInScene<RemoteControlRig>(loaded);
            if (rig == null)
            {
                Debug.LogError($"[ShooterGameManager] The control scene loaded for " + peer.clientID + " has no RemoteControlRig.");
                continue;
            }

            info.controlScene = loaded;
            info.rig = rig;

            // Client -> rig: the rig now reads the input of this clientID and no other.
            rig.Bind(peer.clientID);

            // Rig -> avatar: the avatar now reads this control scene and no other.
            info.controller?.SetControlSource(rig);

            BindCameraToPeer(rig.GetControlCamera(), peer.himuClient);
        }
    }

    private void OffsetScene(Scene scene, int index)
    {
        Vector3 offset = new Vector3(controlSceneOffset * (index + 1), 0f, 0f);

        foreach (GameObject root in scene.GetRootGameObjects())
            root.transform.position += offset;
    }

    private void BindCameraToPeer(Camera camera, HIMUClient peer)
    {
        if (camera == null || peer == null)
        {
            Debug.LogError("[ShooterGameManager] Cannot bind control camera: camera or peer is null.");
            return;
        }

        if (peer.renderTexture == null)
        {
            Debug.LogError($"[ShooterGameManager] Peer " + peer.GetClientID() + " has no RenderTexture assigned.");
            return;
        }

        // Several cameras tagged MainCamera make Camera.main non-deterministic; the control
        // cameras are never the main camera of the application.
        if (camera.CompareTag("MainCamera")) camera.tag = "Untagged";

        camera.targetTexture = peer.renderTexture;
    }

    public void LoadRemoteControlScene(Scene current, Scene next)
    {
        if (next.name != "ShooterScene") return;
        OrganizePeers();
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }

        return null;
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
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.activeSceneChanged -= LoadRemoteControlScene;
            FrameCaptureFeature.Instance?.SetSourceCamera(null);
            Instance = null;
        }
    }

    #endregion
}
