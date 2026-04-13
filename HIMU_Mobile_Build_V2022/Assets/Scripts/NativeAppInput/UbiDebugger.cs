using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;

public class UbiDebugger : MonoBehaviour
{
    [SerializeField]
    DebugInput inputController = null;
    TextMeshProUGUI text = null;
    private double locationTimer = 0.0;
    

    // Start is called before the first frame update
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
        UpdateLocation();
    }

    // Update is called once per frame
    void Update()
    {
        locationTimer += Time.deltaTime;
        if (locationTimer >= 5.0)
        {
            UpdateLocation();
            locationTimer = 0.0;
        }
    }

    void UpdateLocation()
    {
        var coords = inputController.ObtenerCoordenadas();
        text.text = $"Lat: {coords.lat}, Lon: {coords.lon}";
    }
}
