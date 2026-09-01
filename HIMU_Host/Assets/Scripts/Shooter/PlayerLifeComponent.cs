using System.Collections.Generic;
using UnityEngine;

public class PlayerLifeComponent : MonoBehaviour
{

    #region Variables

    private string clientID;

    private int life = 3;

    [SerializeField] private List<GameObject> lifeObjects; 
    
    Queue<GameObject> lifeQueue;

    #endregion

    #region Methods

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
                gameObject.SetActive(false);
            }
        }            
    }

    public void ResetLife()
    {
        for (int i = 0; i < lifeObjects.Count; i++)
            lifeObjects[i].SetActive(true);

        life = 3;
        lifeQueue = new Queue<GameObject>(lifeObjects);
    }

    #endregion

    #region Setters

    public void SetClientID(string id)
    {
        clientID = id;
    }

    #endregion

    #region Monobehaviour

    private void Start()
    {
        lifeQueue = new Queue<GameObject>(lifeObjects);
    }

    #endregion
}
