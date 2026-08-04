using System.Collections.Generic;
using UnityEngine;

public class ShooterManager : MonoBehaviour
{
    #region Variables

    [SerializeField]
    List<Transform> spawnPositions;

    #endregion

    private void OrganizePeers()
    {
        List<ClientData> peers = StreamManager.Instance.GetClients();

        for (int i = 0; i < peers.Count; i++)
        {
            Transform cam = peers[i].himuClient.gameObject.transform;
            cam.SetParent(spawnPositions[i].transform.GetChild(0).transform, false);

            cam.localPosition = new Vector3(0f, 0.6f, 0f);
            cam.localRotation = Quaternion.identity;
        }
    }

    private void Awake()
    {
        OrganizePeers();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
