using UnityEngine;

/// <summary>
/// Moves forward at laserSpeed and destroys itself on hitting the player,
/// a wall, or after maxLifetime seconds.
///
/// Unity Setup:
///   - Attach to the LaserProjectile prefab (the stretched cylinder).
///   - Capsule Collider must have Is Trigger ON.
///   - Assign playerLayer and wallLayer in the Inspector (or leave wallLayer
///     as default to auto-detect "Level 1 Obstacle").
/// </summary>
public class LaserProjectile : MonoBehaviour
{
    [Tooltip("Movement speed of the projectile.")]
    public float laserSpeed = 18f;

    [Tooltip("Damage dealt to the player on hit.")]
    public float damage = 35f;

    [Tooltip("Seconds before the projectile destroys itself if it hits nothing.")]
    public float maxLifetime = 3f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Tooltip("Particle burst prefab to spawn on any hit (player or wall).")]
    public GameObject hitBurstPrefab;

    private int _wallLayer;

    void Start()
    {
        _wallLayer = LayerMask.NameToLayer("Level 1 Obstacle");
        Destroy(gameObject, maxLifetime);
    }

    void Update()
    {
        transform.position += transform.forward * laserSpeed * Time.deltaTime;
    }

    void SpawnBurst()
    {
        if (hitBurstPrefab != null)
            Instantiate(hitBurstPrefab, transform.position, Quaternion.identity);
    }

    void OnTriggerEnter(Collider other)
    {
        // Hit player.
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, transform.position);
                Debug.Log($"[LaserProjectile] Hit player for {damage} damage.");
            }
            SpawnBurst();
            Destroy(gameObject);
            return;
        }

        // Hit wall.
        if (other.gameObject.layer == _wallLayer)
        {
            Debug.Log("[LaserProjectile] Hit wall.");
            SpawnBurst();
            Destroy(gameObject);
        }
    }
}
