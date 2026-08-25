using System;
using System.Collections;
using UnityEngine;
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
        if (!isFading) StartCoroutine(Fade());
    }

    public void SetCallback(Action<string> c)
    {
        callback = c;
    }

    public void SetScene(string scene)
    {
        nextScene = scene;
    }

    private IEnumerator Fade()
    {
        isFading = true;
        while (timer < timeToFade)
        {
            timer += Time.deltaTime;
            Color aux = image.color;
            aux.a = timer / timeToFade;
            image.color = aux;
            yield return null;
        }

        callback?.Invoke(nextScene);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        image = GetComponent<Image>();
    }
}
