using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AppManager : MonoBehaviour
{
    public static AppManager Instance { get; private set; }

    [SerializeField] private float timeToFade = 1.5f;
    [SerializeField] private RawImage fadeOutImage;

    private Color auxColor = Color.black;
    private bool isFading = false;
    private float timer = 0;
    private string nextScene = "";

    private IEnumerator Fade()
    {
        while (timer <= timeToFade)
        {
            timer += Time.deltaTime;
            auxColor = fadeOutImage.color;
            auxColor.a = timer / timeToFade;
            fadeOutImage.color = auxColor;
            yield return null;
        }

        timer = 0.0f;
        auxColor = Color.black;
        auxColor.a = 1;
        fadeOutImage.color = auxColor;
        UnityMainThreadDispatcher.Instance().Enqueue(() => AppManager.Instance.ChangeScene(nextScene));
        if (nextScene.Contains("Menu"))
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => Destroy(UnityMainThreadDispatcher.Instance().gameObject));
            Destroy(ConnectionManager.Instance.gameObject);
        }
        isFading = false;
    }

    private RawImage FindFadeOutInScene()
    {
        foreach (GameObject rootObj in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObj.name.Contains("FadeOut"))
            {
                RawImage img = rootObj.GetComponent<RawImage>();
                if (img != null) return img;
            }

            Transform found = rootObj.transform.Find("FadeOutImage");
            if (found != null)
            {
                RawImage img = found.GetComponent<RawImage>();
                if (img != null) return img;
            }

            // Fallback: cualquier RawImage dentro del árbol de este root
            RawImage anyImg = rootObj.GetComponentInChildren<RawImage>(true);
            if (anyImg != null) return anyImg;
        }

        return null;
    }

    public void StartFading(RawImage image, string scene)
    {
        if (isFading) return;
        isFading = true;
        fadeOutImage = image;
        if (image == null) fadeOutImage = FindFadeOutInScene();
        fadeOutImage.raycastTarget = true;
        nextScene = scene;
        StartCoroutine(Fade());
    }

    public void ExitApp()
    {
        Application.Quit();
    }

    public void ConnectionLost()
    {
        ChangeScene("MainMenuScene");
        Destroy(ConnectionManager.Instance.gameObject);
    }

    public void ChangeScene(string scene)
    {
        SceneManager.LoadScene(scene);
    }

    void Awake()
    {
        if (Instance)
        {
            try { Destroy(Instance.gameObject); }
            catch { Debug.Log("No se pudo borrar el objeto del singleton"); }
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}