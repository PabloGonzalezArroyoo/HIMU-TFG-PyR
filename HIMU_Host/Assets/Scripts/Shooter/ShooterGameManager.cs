using System.Collections.Generic;
using UnityEngine;

public class ShooterGameManager : MonoBehaviour
{
    #region Variables

    [SerializeField]
    List<Transform> spawnPositions;

    int players;

    GameObject victoryCanvas;

    #endregion

    private void OrganizePeers()
    {
        List<ClientData> peers = StreamManager.Instance.GetClients();
        players = peers.Count;

        for (int i = 0; i < peers.Count; i++)
        {
            Transform cam = peers[i].himuClient.gameObject.transform;
            cam.SetParent(spawnPositions[i].transform.GetChild(0).transform, false);

            cam.localPosition = new Vector3(0f, 0.6f, 0f);
            cam.localRotation = Quaternion.identity;
        }
    }

    private void PlayerEliminated()
    {
        players--;

        if (players == 1)
        {
            // TO-DO: cambiar texto de victoria y asignar numero al texto
            victoryCanvas.SetActive(true);
        }
    }

    private void Awake()
    {
        //OrganizePeers();
        victoryCanvas.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
