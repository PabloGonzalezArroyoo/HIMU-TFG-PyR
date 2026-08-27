using UnityEngine;

public class SinMovementComponent : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.25f;
    [SerializeField] private float frequency = 1f;
    [SerializeField] private Vector3 axis = Vector3.up;

    private Vector3 startPosition;
    private float phaseOffset;

    private void Start()
    {
        startPosition = transform.localPosition;
        phaseOffset = Random.Range(0f, Mathf.PI * 2f); 
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f + phaseOffset) * amplitude;
        transform.localPosition = startPosition + axis.normalized * offset;
    }
}