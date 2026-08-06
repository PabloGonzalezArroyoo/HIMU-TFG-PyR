using UnityEngine;

public class ShootingComponent : MonoBehaviour
{
    [SerializeField] private BulletComponent bulletPrefab;
    [SerializeField] private Transform bulletSpawn;

    [SerializeField] private float cooldown = 1f;
    private float timer;

    private void Start()
    {
        timer = 0f;
    }

    private void Update()
    {
        timer += Time.deltaTime;
    }

    public void Shoot()
    {
        Debug.Log("Disparo");
        if (bulletPrefab == null || bulletSpawn == null) return;

        if (timer < cooldown) return;

        BulletComponent bullet = Instantiate(bulletPrefab, bulletSpawn.position, bulletSpawn.rotation);
        bullet.Initialize(bulletSpawn.forward);
        timer = 0f;
    }
}
