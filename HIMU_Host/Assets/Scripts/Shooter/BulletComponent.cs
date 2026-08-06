using UnityEngine;

public class BulletComponent : MonoBehaviour
{
    [SerializeField] private float speed = 20f;

    private Vector3 direction = Vector3.forward;

    public void Initialize(Vector3 dir)
    {
        direction = dir.normalized;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<PlayerLifeComponent>(out var life))
        {
            Debug.Log("Soy un player!");
            life.TakeDamage();
        }

        Destroy(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }
}
