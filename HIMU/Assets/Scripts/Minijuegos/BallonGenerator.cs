using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.XInput;
using UnityEngine.UIElements;

public class BalloonGenerator : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private List<GameObject> balloonPrefab;
    [SerializeField] private BalloonInputController inputController;

    private BalloonComponent previousBalloon = null;

    [Header("Spawn")]
    [SerializeField] private Transform puntoSpawn;   // Si es null, usa la posición del generador

    [SerializeField]
    private TextMeshProUGUI pointsText;

    private void Start()
    {
        InstantiateBalloon();
    }

    IEnumerator SpawnBalloon(Vector3 posicion)
    {
        yield return new WaitForSeconds(1.5f); 
        GameObject nuevoObj = Instantiate(balloonPrefab[Random.Range(0, balloonPrefab.Count)], posicion, Quaternion.identity, gameObject.transform);

        BalloonComponent globo = nuevoObj.GetComponent<BalloonComponent>();

        if (globo == null)
        {
            Debug.LogError("GloboGenerador: el prefab no tiene GloboComponent.");
        }

        globo.OnPoppedBalloon += InstantiateBalloon;

        inputController.SetBalloon(globo);
        previousBalloon = globo;
    }

    private void InstantiateBalloon()
    {
        if(previousBalloon)
        {
            // Sumar puntos a la UI
            if (pointsText != null)
            {
                int puntuacionActual = int.Parse(pointsText.text);
                pointsText.text = "Puntos: " + (puntuacionActual + previousBalloon.GetPoints()).ToString();
            }
        }

        Vector3 posicion = puntoSpawn != null ? puntoSpawn.position : transform.position;
        StartCoroutine(SpawnBalloon(posicion));
    }
}