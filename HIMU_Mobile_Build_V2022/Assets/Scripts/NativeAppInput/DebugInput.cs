using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Android;

public class DebugInput : MonoBehaviour
{
    [SerializeField]
    double timerLimit = 0.5;
    double timer = 0.0;
    public bool debuggingInput = true;
    public bool debuggingMic = true;
    public bool debuggingLocalization = true;

    // Microfono
    private AudioClip micClip;
    private AudioSource audioSource;
    private string micDevice;
    private float micVolume = 0.0f;
    private bool recording = false;
    private double recordingTimer = 0.0;

    [SerializeField]
    private TextMeshProUGUI volumeText; 

    // Acelerometro
    Vector3 mobileAcceleration = new Vector3 (0, 0, 0);

    // Touches
    int touches = 0;
    Touch lastTouch = new Touch();

    // Localizacion
    [SerializeField] private float actualizacionIntervalo = 1f;  // segundos
    [SerializeField] private float precisionDeseada = 10f;        // metros
    [SerializeField] private float tiempoEspera = 10f;            // timeout
    private bool UbicacionActiva = true;
    private LocationInfo UltimaPosicion => Input.location.lastData;

    // Start is called before the first frame update
    void Awake()
    {
#if UNITY_ANDROID
        StartCoroutine(PedirPermisoYIniciar());
        StartCoroutine(IniciarUbicacion());
#else
        IniciarMicrofono();
#endif

        mobileAcceleration = Input.acceleration;

        audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (debuggingInput) {        
            DebugAccelerometer();
        }

        if (debuggingMic) {
            DebugMic(); 
        }
    }

    private void DebugAccelerometer()
    {
        timer += Time.deltaTime;
        if (timer < timerLimit) return;

        timer = 0.0;
        if (mobileAcceleration != Input.acceleration)
        {
            mobileAcceleration = Input.acceleration;
            Debug.Log("Acelerometro del movil: " + mobileAcceleration);
        }

        if (0 < Input.touchCount)
        {
            lastTouch = Input.touches[0];
            Debug.Log("Nuevo touch en pantalla: " + touches + ", posicion del touch: " + lastTouch.position);
        }

        if (audioSource.clip != null && audioSource.clip.loadState == AudioDataLoadState.Loaded)
        {
            audioSource.clip = null;
            Debug.Log("Microfono del movil capturado: " + micVolume);
        }
    }

    private void DebugMic()
    {
        if (recording)
        {
            recordingTimer += Time.deltaTime;
            if (recordingTimer >= 5.0)
            {
                recordingTimer = 0.0;
                recording = false;
                audioSource.Play();
                volumeText.text = "New volume was: " + GetMicrophoneVolume();
            }
        }
    }

    #region Microfono
    void StartMicrophone()
    {
        // Parámetros: dispositivo, loop, duración del buffer (seg), frecuencia de muestreo
        micClip = Microphone.Start(micDevice, true, 5, 44100);
        audioSource.clip = micClip;
        audioSource.loop = false;

        // Esperar a que el micrófono empiece antes de reproducir
        while (!(Microphone.GetPosition(micDevice) > 0)) { }

        
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
            Application.Quit();
        }
    }

    public void StartRecording()
    {
        recording = true;
        StartMicrophone();
    }
    #endregion

    #region Localizacion

    private IEnumerator IniciarUbicacion()
    {
        // ── 1. Permisos en Android ──────────────────────────────────────
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);

            // Esperar hasta que el usuario responda al diálogo
            yield return new WaitUntil(() =>
                Permission.HasUserAuthorizedPermission(Permission.FineLocation) ||
                !Permission.HasUserAuthorizedPermission(Permission.FineLocation));

            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Debug.LogWarning("[Ubicación] Permiso denegado en Android.");
                yield break;
            }
        }
#endif

        // ── 2. Comprobar que el servicio no está ya corriendo ───────────
        if (Input.location.status == LocationServiceStatus.Running)
        {
            UbicacionActiva = true;
            yield break;
        }

        // ── 3. Iniciar el servicio ──────────────────────────────────────
        Input.location.Start(precisionDeseada, precisionDeseada);

        float tiempoInicio = Time.time;
        while (Input.location.status == LocationServiceStatus.Initializing)
        {
            if (Time.time - tiempoInicio > tiempoEspera)
            {
                Debug.LogError("[Ubicación] Timeout al iniciar el servicio de ubicación.");
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("[Ubicación] No se pudo iniciar el servicio de ubicación.");
            yield break;
        }

        // ── 5. ¡Listo! ─────────────────────────────────────────────────
        UbicacionActiva = true;
        Debug.Log($"[Ubicación] Servicio activo. Precisión: {precisionDeseada}m");

        StartCoroutine(ActualizarUbicacion());
    }

    private IEnumerator ActualizarUbicacion()
    {
        while (UbicacionActiva)
        {
            var pos = Input.location.lastData;
            Debug.Log($"[Ubicación] Lat: {pos.latitude:F6} | Lon: {pos.longitude:F6} | Alt: {pos.altitude:F1}m | Precisión: {pos.horizontalAccuracy:F1}m");
            yield return new WaitForSeconds(actualizacionIntervalo);
        }
    }

    // ── API pública ────────────────────────────────────────────────────
    public (double lat, double lon) ObtenerCoordenadas()
    {
        if (!UbicacionActiva) return (0, 0);
        return (UltimaPosicion.latitude, UltimaPosicion.longitude);
    }

    #endregion


    void OnDestroy()
    {
        StopMicrophone();
        if (Input.location.status == LocationServiceStatus.Running)
            Input.location.Stop();
    }
}
