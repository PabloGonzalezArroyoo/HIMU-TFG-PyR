using UnityEngine;

public class BalloonInputController : MonoBehaviour
{
    [Header("Configuración del micrófono")]
    [SerializeField] private float umbralVolumen = 0.05f;   // 0.0 – 1.0
    [SerializeField] private float sensibilidad = 50f;       // Aire por segundo al soplar
    protected float volume = 0f;
    private BalloonComponent globoActual;

    private void Update()
    {
        if (globoActual == null) return;

        if (volume > umbralVolumen)
        {
            float aire = (volume - umbralVolumen) * sensibilidad * Time.deltaTime;
            globoActual.Blow(aire);
        }
    }

    public void SetVolume(float v)
    {
        volume = v;
    }

    /// <summary>
    /// Llamado por el generador cada vez que instancia un nuevo globo.
    /// </summary>
    public void SetBalloon(BalloonComponent nuevoGlobo)
    {
        globoActual = nuevoGlobo;
    }

    private void OnDestroy()
    {
        Microphone.End(null);
    }
}