using UnityEngine;

/// <summary>
/// Test component that will move an object in a circle repeatedly.
/// Used for the camera so that each frame rendered has differences.
/// </summary>
public class MovementTestComponent : MonoBehaviour
{
    public float radius;
    public float speed;

    private Vector3 center;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        center = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        float angle = Time.time * speed;

        float x = center.x + Mathf.Cos(angle) * radius;
        float z = center.z + Mathf.Sin(angle) * radius;

        transform.position = new Vector3(x, center.y, z);
    }
}
