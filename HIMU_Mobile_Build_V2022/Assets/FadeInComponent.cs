using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fade out the GameObject that contains this component.
/// Attach it to any GameObject that has an Image component.
/// Call StartFade() from code or trigger it from the Inspector via UnityEvent.
/// </summary>
[RequireComponent(typeof(Image))]
public class FadeInComponent : MonoBehaviour
{
    [SerializeField]
    protected float duration = 1f;
    [SerializeField]
    protected bool playOnStart = false;

    private Image _image;
    private Coroutine _currentFade;

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void Start()
    {
        if (playOnStart)
            StartFade();
    }

    public void StartFade()
    {
        if (_currentFade != null)
            StopCoroutine(_currentFade);

        _currentFade = StartCoroutine(FadeCoroutine());
    }

    private IEnumerator FadeCoroutine()
    {
        // Ensure the object is fully visible before fading
        Color startColor = _image.color;
        startColor.a = 1f;
        _image.color = startColor;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - elapsed / duration);

            Color c = _image.color;
            c.a = alpha;
            _image.color = c;

            yield return null;
        }

        // Guarantee fully transparent at the end
        Color final = _image.color;
        final.a = 0f;
        _image.color = final;

        _currentFade = null;

        gameObject.SetActive(false);
    }
}