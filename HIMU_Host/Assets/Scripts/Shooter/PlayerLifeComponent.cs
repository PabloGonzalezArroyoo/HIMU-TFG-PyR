using System.Collections.Generic;
using UnityEngine;

public class PlayerLifeComponent : MonoBehaviour
{
    private int life = 3;

    [SerializeField]
    Queue<GameObject> lifeQueue;

    public void RemoveLife()
    {
        if (life > 0)
        {
            life--;
            GameObject lifeObject = lifeQueue.Dequeue();
            lifeObject.SetActive(false);
        }
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
