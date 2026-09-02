using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DisplayCanvasController : MonoBehaviour
{
    #region Variables
    /// <summary>
    /// Reference to teh UI element used for fade out
    /// </summary>
    [SerializeField] private RawImage fadeOutImage;
    /// <summary>
    /// Reference to the canvas where we apply the video received
    /// </summary>
    [SerializeField] private Canvas canvas;
    #endregion

    #region Methods
    /// <summary>
    /// Method that finds the camera in the current scene
    /// </summary>
    /// <param name="scene">Scene where to search for the camera</param>
    /// <param name="cameraName">Camera name</param>
    /// <returns></returns>
    private Camera FindCameraInScene(UnityEngine.SceneManagement.Scene scene, string cameraName)
    {
        foreach (GameObject rootObj in scene.GetRootGameObjects())
        {
            if (!string.IsNullOrEmpty(cameraName))
            {
                if (rootObj.name == cameraName)
                {
                    Camera cam = rootObj.GetComponent<Camera>();
                    if (cam != null) return cam;
                }

                Transform found = rootObj.transform.Find(cameraName);
                if (found != null)
                {
                    Camera cam = found.GetComponent<Camera>();
                    if (cam != null) return cam;
                }
            }

            Camera anyCam = rootObj.GetComponentInChildren<Camera>(true);
            if (anyCam != null) return anyCam;
        }

        return null;
    }

    /// <summary>
    /// Method that spoecifies what to do when we transition to main scene. We activate the process to send input and set the camera to render the video we receive
    /// </summary>
    /// <param name="current">Current scene</param>
    /// <param name="next">Next scene</param>
    private void OnGameStarted(Scene current, Scene next)
    {
        if (next.name.Contains("Game")) {
            ClientInputManager.Instance.send = true;
            canvas.worldCamera = FindCameraInScene(next, "Main camera");
            canvas.gameObject.SetActive(true);
            SceneManager.activeSceneChanged -= OnGameStarted;
        }
    }
    #endregion

    #region Monobehaviour
    private void Start()
    {
        canvas.gameObject.SetActive(false);
        SceneManager.activeSceneChanged += OnGameStarted;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ConnectionManager.Instance.Disconnect();
            AppManager.Instance.StartFading(fadeOutImage, "MainMenuScene");
        }
    }
    #endregion
}
