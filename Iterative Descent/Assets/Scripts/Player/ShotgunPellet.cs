using UnityEngine;

/// <summary>
/// Single shotgun pellet projectile. Also doubles as the pistol bullet and rifle
/// round -- see notifyDDAOnHit vs onResolved below for how each weapon reports
/// hits back to the DDA accuracy signal.
///
/// Setup:
///   Create a prefab with a small SphereCollider (radius ~0.04), Rigidbody,
///   and this script. Tag it "Pellet" and put it on a dedicated "Pellet" physics layer
///   that does NOT collide with the Player layer (Physics Layer Matrix in Project Settings).
///
/// ShotgunController/PlayerCombat/RifleController set damage and impactPrefab at spawn
/// time then call Launch().
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ShotgunPellet : MonoBehaviour
{
    // ─── Set by spawner at runtime ───────────────────────────────────────────────

    [HideInInspector] public float      damage           = 15f;
    [HideInInspector] public GameObject impactPrefab;
    /// <summary>
    /// Set to true for single-bullet weapons (pistol, rifle) so that the DDA accuracy
    /// signal (NotifyShotLanded) fires immediately on hit. Leave false for shotgun
    /// pellets -- multiple pellets per shot would inflate the accuracy counter; the
    /// shotgun instead uses onResolved below to tally a whole volley at once.
    /// </summary>
    [HideInInspector] public bool notifyDDAOnHit = false;
    /// <summary>
    /// Optional per-pellet resolution callback, invoked exactly once with whether this
    /// pellet hit an IDamageable (true) or not (false). Fired from OnDestroy so it
    /// covers both outcomes -- a collision (with or without a damageable) and a pellet
    /// that flies out to maxLifetime without hitting anything at all. Used by
    /// ShotgunController to tally a whole shot's pellet spread for the DDA accuracy
    /// signal (2026-09: a shot counts as a "landed" hit only if at least
    /// pelletHitFractionForDDA of its pellets connect). Left null for weapons using the
    /// simpler immediate notifyDDAOnHit flag (pistol, rifle).
    /// </summary>
    [HideInInspector] public System.Action<bool> onResolved;

    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Tooltip("Seconds before the pellet self-destructs even if it hits nothing.")]
    public float maxLifetime = 0.6f;

    // ─── Private ─────────────────────────────────────────────────────────────────

    private Rigidbody _rb;
    private bool      _consumed;
    private bool      _hitDamageable;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity               = false;
        _rb.interpolation             = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode    = CollisionDetectionMode.Continuous;

        Destroy(gameObject, maxLifetime);
    }

    void OnDestroy()
    {
        onResolved?.Invoke(_hitDamageable);
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
            _hitDamageable = true;
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
