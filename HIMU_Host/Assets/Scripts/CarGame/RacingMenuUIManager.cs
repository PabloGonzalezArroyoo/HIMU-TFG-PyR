using System;
using UnityEngine;
using UnityEngine.UI;

public class RacingMenuUIManager : MonoBehaviour
{
    public static RacingMenuUIManager Instance { get; private set; }

    [SerializeField]
    private FadeOutComponent fadeOutImage;

    public void ChangeToConnections(Action<string> callback)
    {
        fadeOutImage.StartFading();
        fadeOutImage.SetCallback(callback);
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
