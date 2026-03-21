using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;

public class DebugInput : MonoBehaviour
{
    [SerializeField]
    double timerLimit = 0.5;
    double timer = 0.0;

    // Microfono
    private AudioClip micClip;
    private AudioSource audioSource;
    private string micDevice;
    private float micVolume = 0.0f;

    // Acelerometro
    Vector3 mobileAcceleration = new Vector3 (0, 0, 0);

    // Touches
    int touches = 0;
    Touch lastTouch = new Touch();

    // Start is called before the first frame update
    void Start()
    {
#if UNITY_ANDROID
        StartCoroutine(PedirPermisoYIniciar());
#else
        IniciarMicrofono();
#endif

        mobileAcceleration = Input.acceleration;

        audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;
        if (timer < timerLimit) return;


        timer = 0.0;
        if (mobileAcceleration != Input.acceleration)
        {
            mobileAcceleration = Input.acceleration;
            Debug.Log("Acelerometro del movil: " + mobileAcceleration);
        }

        if (audioSource.clip != null && audioSource.clip.loadState == AudioDataLoadState.Loaded)
        {
            audioSource.clip = null;
            Debug.Log("Microfono del movil capturado: " + micVolume);
        }

        if (0 < Input.touchCount)
        {
            lastTouch = Input.touches[0];
            Debug.Log("Nuevo touch en pantalla: " + touches + ", posicion del touch: " + lastTouch.position);
        }
    }

    void StartMicrophone()
    {
        // Parámetros: dispositivo, loop, duración del buffer (seg), frecuencia de muestreo
        micClip = Microphone.Start(micDevice, true, 10, 44100);
        audioSource.clip = micClip;
        audioSource.loop = true;

        // Esperar a que el micrófono empiece antes de reproducir
        while (!(Microphone.GetPosition(micDevice) > 0)) { }

        micVolume = GetMicrophoneVolume();
        //audioSource.Play();
    }

    void StopMicrophone()
    {
        Microphone.End(micDevice);
        audioSource.Stop();
    }

    float GetMicrophoneVolume()
    {
        float[] samples = new float[128];
        int micPosition = Microphone.GetPosition(micDevice) - 128;
        if (micPosition < 0) return 0;

        micClip.GetData(samples, micPosition);

        float sum = 0f;
        foreach (float sample in samples)
            sum += sample * sample;

        return Mathf.Sqrt(sum / samples.Length); // RMS (volumen)
    }

    private System.Collections.IEnumerator PedirPermisoYIniciar()
    {
        // Pedir permiso si no lo tiene
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);

            // Esperar hasta que el usuario responda
            yield return new WaitUntil(() =>
                Permission.HasUserAuthorizedPermission(Permission.Microphone));
        }

        IniciarMicrofono();
    }

    private void IniciarMicrofono()
    {
        // Log de todos los dispositivos detectados
        Debug.Log("Dispositivos encontrados: " + Microphone.devices.Length);
        foreach (string device in Microphone.devices)
            Debug.Log("  → " + device);

        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            micClip = Microphone.Start(micDevice, true, 10, 44100);
            audioSource.clip = micClip;
            audioSource.loop = true;

            while (!(Microphone.GetPosition(micDevice) > 0)) { }
            audioSource.Play();
        }
        else
        {
            Debug.LogError("No se encontró ningún micrófono.");
        }
    }

    void OnDestroy()
    {
        StopMicrophone();
    }
}
