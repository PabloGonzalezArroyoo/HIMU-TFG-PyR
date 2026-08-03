using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TestScenesBackground : MonoBehaviour
{
    private RenderTexture targetTexture;

    [SerializeField] private string backgroundSceneName;

    [SerializeField] private string backgroundCameraName = "Main Camera";

    private GraphicRaycaster backgroundRaycaster;

    private Camera backgroundCamera;

    private UnityEngine.SceneManagement.Scene loadedScene;

    void Start()
    {
        StartCoroutine(LoadBackgroundSceneAsync());
    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector3 pos = Mouse.current.position.ReadValue();
            TryClickBackgroundUI(pos);
        }
    }

    /// <summary>
    /// Lanza un raycast al recibir un vector posicion que representa un click o toque de un cliente
    /// </summary>
    /// <param name="screenPosition">Posicion del click donde debemos lanzar el raycast (viene ya normalizada)</param>
    private void TryClickBackgroundUI(Vector2 screenPosition)
    {
        if (backgroundRaycaster == null)
        {
            Debug.Log("No hay referencia al raycaster del canvas");
            return;
        }

        // Desnormalizamos las coordenadas del click
        Vector2 virtualScreenPos = new Vector2(screenPosition.x * targetTexture.width, screenPosition.y * targetTexture.height);

        // Lanzamos el raycast
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = virtualScreenPos};
        List<RaycastResult> results = new List<RaycastResult>();
        backgroundRaycaster.Raycast(pointerData, results);

        // Recorremos todos los objetos que detecta el raycast
        if (results.Count > 0)
        {
            for (int i = 0; i < results.Count; i++)
            {
                GameObject target = results[i].gameObject;
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);  // El propio click
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler);   // Cambiar estado visual a pressed
                ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);     // Cambiar estado visual a normal
            }
        }
    }

    private IEnumerator LoadBackgroundSceneAsync()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(backgroundSceneName, LoadSceneMode.Additive);

        // Esperamos a que termine de cargar
        while (!op.isDone)
        {
            yield return null;
        }

        loadedScene = SceneManager.GetSceneByName(backgroundSceneName);

        backgroundCamera = FindCameraInScene(loadedScene);

        backgroundRaycaster = GameObject.Find("RemoteControl_Canvas").GetComponent<GraphicRaycaster>();

        if (backgroundCamera != null)
        {
            backgroundCamera.targetTexture = targetTexture;
        }
        else
        {
            Debug.LogWarning($"No se encontró ninguna cámara en la escena '{backgroundSceneName}'.");
        }
    }

    private Camera FindCameraInScene(UnityEngine.SceneManagement.Scene scene)
    {
        // Recorremos los objetos raíz de la escena buscando la cámara
        foreach (GameObject rootObj in scene.GetRootGameObjects())
        {
            // Si se especificó un nombre concreto, priorizamos búsqueda exacta
            if (!string.IsNullOrEmpty(backgroundCameraName))
            {
                if (rootObj.name == backgroundCameraName)
                {
                    Camera cam = rootObj.GetComponent<Camera>();
                    if (cam != null) return cam;
                }

                Transform found = rootObj.transform.Find(backgroundCameraName);
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
}
