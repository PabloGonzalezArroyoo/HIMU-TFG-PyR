using System.Collections.Generic;
using UnityEngine;

public class PlayerLifeComponent : MonoBehaviour
{
    private string clientID;

    private int life = 3;

    [SerializeField] private List<GameObject> lifeObjects; 
    
    Queue<GameObject> lifeQueue;

    public void SetClientID(string id)
    {
        clientID = id;
    }

    private void Start()
    {
        lifeQueue = new Queue<GameObject>(lifeObjects);
    }

    public void TakeDamage()
    {
        if (life > 0)
        {
            life--;
            GameObject lifeObject = lifeQueue.Dequeue();
            lifeObject.SetActive(false);

            if (life <= 0)
            {
                ShooterGameManager.Instance.PlayerEliminated(clientID);
                Destroy(gameObject);
            }
        }            
    }
}
