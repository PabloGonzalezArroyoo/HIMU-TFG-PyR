using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BalloonComponent : MonoBehaviour
{
    [Header("Configuración del globo")]
    [SerializeField] private float airLimit = 100f;
    [SerializeField] private float initialScale = 0.5f;
    [SerializeField] private float maximunScale = 2.5f;
    [SerializeField] private int pointsToScore = 10;

    [Header("Referencias")]
    [SerializeField] private AudioClip sfx;
    [SerializeField] private TextMeshProUGUI pointsText;

    private float currentAir = 0f;
    private AudioSource audioSource;

    // Evento que el generador escuchará para saber que el globo explotó
    public System.Action OnPoppedBalloon;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        ChangeScale();
    }

    public void Blow(float cantidad)
    {
        currentAir += cantidad;
        currentAir = Mathf.Clamp(currentAir, 0f, airLimit);

        ChangeScale();

        if (currentAir >= airLimit)
            Explode();
    }

    private void ChangeScale()
    {
        float t = currentAir / airLimit;
        float escala = Mathf.Lerp(initialScale, maximunScale, t);
        transform.localScale = Vector3.one * escala;
    }

    private void Explode()
    {
        // Sumar puntos a la UI
        if (pointsText != null)
        {
            int puntuacionActual = int.Parse(pointsText.text);
            pointsText.text = "Puntos: " + (puntuacionActual + pointsToScore).ToString();
        }

        // Reproducir sonido de explosión en una fuente independiente para que no muera con el objeto
        if (sfx != null)
            AudioSource.PlayClipAtPoint(sfx, transform.position);

        // Notificar al generador
        OnPoppedBalloon?.Invoke();

        Destroy(gameObject);
    }
}