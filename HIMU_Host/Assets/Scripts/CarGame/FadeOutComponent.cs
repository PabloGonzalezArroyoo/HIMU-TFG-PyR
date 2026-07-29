using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FadeOutComponent : MonoBehaviour
{
    private Image image;

    [SerializeField]
    private float timeToFade = 2f;
    private float timer = 0f;

    private bool isFading = false;

    [SerializeField]
    private string nextScene = "";

    private Action<string> callback;

    public void StartFading()
    {
        if (!isFading)
        {
            isFading = true;
            SceneManager.LoadSceneAsync(nextScene);
        }
    }

    public void SetCallback(Action<string> c)
    {
        callback = c;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        image = GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {
        if (isFading)
        {
            timer += Time.deltaTime;
            Color aux = image.color;
            aux.a = timer / timeToFade;
            image.color = aux;
            if (aux.a >= 1)
            {
                callback(nextScene);
            }
        }
    }
}
