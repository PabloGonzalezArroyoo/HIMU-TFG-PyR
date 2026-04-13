using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField]
    private Image fadeOutImage = null;

    public List<String> scenes = new List<String> { "MobileUSBConnection", "MobileWifiGamepad", "MobileWifiMultiplayer"};

    public float fadeOutTimer = 3.5f;
    private float timer = 0f;
    private bool isFading = false;
    private string nextScene = "";

    // Update is called once per frame
    void Update()
    {
        if (isFading) {
            timer += Time.deltaTime;
            fadeOutImage.color = new Color(0,0,0, timer/fadeOutTimer);
            if (timer >= fadeOutTimer)
            {
                isFading = false;
                timer = 0f;
            }
        }
    }

    public void ChangeToConnectionScene(int connectionType)
    {
        if (!isFading)
        {
            nextScene = scenes[connectionType];
            isFading = true;
            fadeOutImage.raycastTarget = true;
            StartCoroutine(StartChangeOfScene());
        }
    }

    IEnumerator StartChangeOfScene()
    {
        Debug.Log("Changing scene: " + nextScene);
        yield return new WaitForSeconds(fadeOutTimer);
        SceneManager.LoadScene(nextScene);
    }
}
