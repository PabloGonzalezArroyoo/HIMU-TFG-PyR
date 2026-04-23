using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem.XInput;

public class GloboGenerador : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private List<GameObject> balloonPrefab;
    [SerializeField] private BalloonInputController inputController;

    [Header("Spawn")]
    [SerializeField] private Transform puntoSpawn;   // Si es null, usa la posición del generador

    private void Start()
    {
        InstantiateBalloon();
    }

    private void InstantiateBalloon()
    {
        Vector3 posicion = puntoSpawn != null ? puntoSpawn.position : transform.position;
        GameObject nuevoObj = Instantiate(balloonPrefab[Random.Range(0, balloonPrefab.Count)], posicion, Quaternion.identity);

        BalloonComponent globo = nuevoObj.GetComponent<BalloonComponent>();

        if (globo == null)
        {
            Debug.LogError("GloboGenerador: el prefab no tiene GloboComponent.");
            return;
        }

        // Suscribirse al evento de explosión para generar el siguiente globo
        globo.OnPoppedBalloon += InstantiateBalloon;

        // Pasar la referencia al InputController
        inputController.SetBalloon(globo);
    }
}