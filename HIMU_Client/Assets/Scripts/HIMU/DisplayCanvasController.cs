using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DisplayCanvasController : MonoBehaviour
{
    [SerializeField] private RawImage fadeOutImage;
    [SerializeField] private Canvas canvas;

    private Camera FindCameraInScene(UnityEngine.SceneManagement.Scene scene, string cameraName)
    {
        // Recorremos los objetos raíz de la escena buscando la cámara
        foreach (GameObject rootObj in scene.GetRootGameObjects())
        {
            // Si se especificó un nombre concreto, priorizamos búsqueda exacta
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

            // Fallback: cualquier Camera dentro del árbol de este root
            Camera anyCam = rootObj.GetComponentInChildren<Camera>(true);
            if (anyCam != null) return anyCam;
        }

        return null;
    }

    private void OnGameStarted(Scene current, Scene next)
    {
        if (next.name.Contains("Game")) {
            InputManager.Instance.send = true;
            canvas.worldCamera = FindCameraInScene(next, "Main camera");
            canvas.gameObject.SetActive(true);
            SceneManager.activeSceneChanged -= OnGameStarted;
        }
    }

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
}
