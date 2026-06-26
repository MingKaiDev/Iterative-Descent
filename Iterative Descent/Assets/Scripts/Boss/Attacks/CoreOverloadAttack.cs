using System.Collections;
using UnityEngine;

/// <summary>
/// DROID-7 Core Overload attack -- fires a laser beam from the head toward the player.
/// Deals damage on hit and spawns a particle burst at the impact point.
///
/// Animation Event setup (add to the triggerCoreOverload clip in DROIDAnimator):
///   - "OnHitboxOpen"    -- frame where the laser fires
///   - "OnAttackAnimEnd" -- last frame of the clip (signals attack done)
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject.
///   - Assign laserOrigin (the child GO on the head with the LineRenderer).
///   - Assign hitBurstPrefab (your particle burst prefab).
///   - Assign playerLayer in the Inspector.
/// </summary>
public class CoreOverloadAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Core Overload Settings")]
    [Tooltip("Damage dealt to the player on hit.")]
    public float damage = 35f;

    [Tooltip("Maximum range of the laser raycast.")]
    public float laserRange = 20f;

    [Tooltip("How long the beam stays visible after firing (seconds).")]
    public float beamDuration = 0.3f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Header("References")]
    [Tooltip("The child GameObject on the head containing the LineRenderer.")]
    public Transform laserOrigin;

    [Tooltip("Particle burst prefab to spawn at the hit point.")]
    public GameObject hitBurstPrefab;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerCoreOverload = Animator.StringToHash("triggerCoreOverload");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator     _animator;
    private LineRenderer _lineRenderer;
    private Transform    _player;

    void Start()
    {
        _animator = GetComponent<Animator>();

        if (laserOrigin != null)
        {
            _lineRenderer = laserOrigin.GetComponent<LineRenderer>();
            if (_lineRenderer == null)
                Debug.LogError("[CoreOverloadAttack] No LineRenderer found on LaserOrigin.");
        }
        else
        {
            Debug.LogError("[CoreOverloadAttack] LaserOrigin is not assigned.");
        }

        var playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
            _player = playerHealth.transform;

        // Beam starts hidden.
        if (_lineRenderer != null)
            _lineRenderer.enabled = false;
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerCoreOverload);
        Debug.Log("[CoreOverloadAttack] Core Overload triggered.");
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    public override void OnHitboxOpen()
    {
        if (!IsActive) return;
        if (_lineRenderer == null || laserOrigin == null) return;

        // Direction from head toward player (or forward if no player).
        Vector3 origin = laserOrigin.position;
        // Aim at player chest height, not feet.
        Vector3 playerCenter = _player != null
            ? _player.position + Vector3.up * 1f
            : origin + laserOrigin.forward * laserRange;
        Vector3 direction = _player != null
            ? (playerCenter - origin).normalized
            : laserOrigin.forward;

        // Raycast to find hit point.
        Vector3 endPoint;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, laserRange, playerLayer))
        {
            endPoint = hit.point;

            // Deal damage.
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.point);
                Debug.Log($"[CoreOverloadAttack] Hit {hit.collider.gameObject.name} for {damage} damage.");
            }

            // Spawn particle burst at hit point.
            if (hitBurstPrefab != null)
                Instantiate(hitBurstPrefab, hit.point, Quaternion.LookRotation(-direction));
        }
        else
        {
            // No hit -- beam travels to max range.
            endPoint = origin + direction * laserRange;
        }

        // Show beam.
        _lineRenderer.SetPosition(0, origin);
        _lineRenderer.SetPosition(1, endPoint);
        _lineRenderer.enabled = true;

        StartCoroutine(HideBeamAfterDelay());
    }

    // ─── Internal ────────────────────────────────────────────────────────────────

    IEnumerator HideBeamAfterDelay()
    {
        yield return new WaitForSeconds(beamDuration);
        if (_lineRenderer != null)
            _lineRenderer.enabled = false;
    }

    // ─── Override to prevent accidental broadcast ─────────────────────────────────
    public override void OnHitboxClose() { }
}
