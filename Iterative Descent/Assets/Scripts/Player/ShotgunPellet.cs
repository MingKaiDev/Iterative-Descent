using UnityEngine;

/// <summary>
/// Single shotgun pellet projectile.
///
/// Setup:
///   Create a prefab with a small SphereCollider (radius ~0.04), Rigidbody,
///   and this script. Tag it "Pellet" and put it on a dedicated "Pellet" physics layer
///   that does NOT collide with the Player layer (Physics Layer Matrix in Project Settings).
///
/// ShotgunController sets damage and impactPrefab at spawn time then calls Launch().
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ShotgunPellet : MonoBehaviour
{
    // ─── Set by spawner at runtime ───────────────────────────────────────────────

    [HideInInspector] public float      damage           = 15f;
    [HideInInspector] public GameObject impactPrefab;
    /// <summary>
    /// Set to true for single-bullet weapons (pistol) so that the DDA accuracy
    /// signal (NotifyShotLanded) fires on hit. Leave false for shotgun pellets --
    /// multiple pellets per shot would inflate the accuracy counter.
    /// </summary>
    [HideInInspector] public bool notifyDDAOnHit = false;

    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Tooltip("Seconds before the pellet self-destructs even if it hits nothing.")]
    public float maxLifetime = 0.6f;

    // ─── Private ─────────────────────────────────────────────────────────────────

    private Rigidbody _rb;
    private bool      _consumed;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity               = false;
        _rb.interpolation             = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode    = CollisionDetectionMode.Continuous;

        Destroy(gameObject, maxLifetime);
    }

    // ─── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Give the pellet its velocity. Call immediately after Instantiate.
    /// </summary>
    public void Launch(Vector3 direction, float speed)
    {
        _rb.linearVelocity = direction.normalized * speed;
    }

    // ─── Collision ───────────────────────────────────────────────────────────────

    void OnCollisionEnter(Collision collision)
    {
        if (_consumed) return;
        _consumed = true;

        // Apply damage if we hit something damageable.
        IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage, collision.contacts[0].point);
            if (notifyDDAOnHit)
                PlayerMetricsTracker.Instance?.NotifyShotLanded();
        }

        // Spawn impact decal / particle at hit surface.
        if (impactPrefab != null)
        {
            ContactPoint contact = collision.contacts[0];
            Instantiate(impactPrefab, contact.point, Quaternion.LookRotation(contact.normal));
        }

        Destroy(gameObject);
    }
}
