using UnityEngine;

public class MicInput : MonoBehaviour
{
    [Header("Configuración del micrófono")]
    [SerializeField] private int frecuenciaMuestra = 44100;
    private AudioClip clipMicrofono;

    private void Start()
    {
        StartMicrophone();
    }

    private void Update()
    {
        InputManager.Instance.AddInputEvent(GetVolume());
    }

    private void StartMicrophone()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("InputController: No se detectó ningún micrófono.");
            return;
        }

        // Grabación en bucle continuo de 1 segundo
        clipMicrofono = Microphone.Start(null, true, 1, frecuenciaMuestra);
    }

    public float GetVolume()
    {
        int posicion = Microphone.GetPosition(null) - 128;
        if (posicion < 0) posicion = 0;

        float[] muestras = new float[128];
        clipMicrofono.GetData(muestras, posicion);

        float suma = 0f;
        foreach (float muestra in muestras)
            suma += muestra * muestra;

        return Mathf.Sqrt(suma / muestras.Length);
    }
}