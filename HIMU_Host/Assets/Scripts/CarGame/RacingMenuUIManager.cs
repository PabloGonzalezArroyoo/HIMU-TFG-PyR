using System;
using UnityEngine;
using UnityEngine.UI;

public class RacingMenuUIManager : MonoBehaviour
{
    public static RacingMenuUIManager Instance { get; private set; }

    [SerializeField]
    private FadeOutComponent fadeOutImage;

    public void GoToConnections()
    {
        fadeOutImage.StartFading();
        fadeOutImage.SetCallback(RacingGameManager.Instance.ChangeScene);
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
