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

    public void StartFading(string sceneName, Action<string> c)
    {
        if (!isFading)
        {
            StartCoroutine(Fade());
            callback = c;
            nextScene = sceneName;
            timer = 0f;
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = true;
        }
    }

    public void CancelFade()
    {
        isFading = false;
        callback = null;
        nextScene = "";
        timer = 0f;
        image.color = new Color(0,0,0,0);
        image.raycastTarget = false;
    }

    private IEnumerator Fade()
    {
        isFading = true;
        while (isFading && timer < timeToFade)
        {
            timer += Time.deltaTime;
            Color aux = image.color;
            aux.a = timer / timeToFade;
            image.color = aux;
            yield return null;
        }

        if (isFading) callback?.Invoke(nextScene);
        else
        {
            timer = 0f;
            image.color = new Color(0, 0, 0, 0);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        image = GetComponent<Image>();
    }
}
