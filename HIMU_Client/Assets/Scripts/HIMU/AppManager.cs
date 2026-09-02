using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Class in charge of scene transition and app events (exit and connection lost)
/// </summary
public class AppManager : MonoBehaviour
{
    #region Variables
    /// <summary>
    /// Instance of AppManager (Singleton)
    /// </summary>
    public static AppManager Instance { get; private set; }

    /// <summary>
    /// Specified time for fade out effect
    /// </summary>
    [SerializeField] private float timeToFade = 1.5f;

    /// <summary>
    /// Reference to UI Elemnt used for fade out effect
    /// </summary>
    [SerializeField] private RawImage fadeOutImage;

    /// <summary>
    /// Color auxiliar variable used to change image opacity during fade out effect 
    /// </summary>
    private Color auxColor = Color.black;

    /// <summary>
    /// Variable that controls if the script is currently during a fade out process
    /// </summary>
    private bool isFading = false;

    /// <summary>
    /// Variable used to change image opacity and whether the fade out is completed or not
    /// </summary>
    private float timer = 0;

    /// <summary>
    /// Name of the scene we changed to when fade out is completed
    /// </summary>
    private string nextScene = "";
    #endregion

    #region Fade out effect
    /// <summary>
    /// Coroutine used for fade out effect, it executes 
    /// </summary>
    /// <returns></returns>
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

    /// <summary>
    /// Method that finds an Image object in the current scene when it has not been specified for the fade out effect
    /// </summary>
    /// <returns></returns>
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

            RawImage anyImg = rootObj.GetComponentInChildren<RawImage>(true);
            if (anyImg != null) return anyImg;
        }

        return null;
    }

    /// <summary>
    /// Method that initiates fade out
    /// </summary>
    /// <param name="image">UI element used for fade out</param>
    /// <param name="scene">Scene we are transitionating to</param>
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
    #endregion

    #region Events
    /// <summary>
    /// Method used to close the app
    /// </summary>
    public void ExitApp()
    {
        Application.Quit();
    }

    /// <summary>
    /// Method that contains what to do when we lost connection
    /// </summary>
    public void ConnectionLost()
    {
        ChangeScene("MainMenuScene");
        Destroy(ConnectionManager.Instance.gameObject);
    }

    /// <summary>
    /// Method used for changing scene
    /// </summary>
    /// <param name="scene">Scene we change to</param>
    public void ChangeScene(string scene)
    {
        SceneManager.LoadScene(scene);
    }
    #endregion

    void Awake()
    {
        if (Instance != null) 
            Destroy(Instance.gameObject);
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}