using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ShooterUIManager : MonoBehaviour
{
    public static ShooterUIManager Instance { get; private set; }


    public void GoToConnections()
    {
        SceneManager.LoadScene("ShooterConnectionsScene");
    }

    private void Awake()
    {
        if (Instance)
        {
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
    }
}
