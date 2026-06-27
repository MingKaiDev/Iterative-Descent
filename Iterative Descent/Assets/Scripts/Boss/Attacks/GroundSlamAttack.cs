using UnityEngine;

/// <summary>
/// DROID-7 Ground Slam attack -- AOE damage in a radius at the boss's feet.
/// Spawns an expanding shockwave ring and debris burst on impact.
///
/// Animation Event setup (add to the triggerGroundSlam clip in DROIDAnimator):
///   - "OnHitboxOpen"    -- frame where the slam hits the ground (deals damage + spawns VFX)
///   - "OnAttackAnimEnd" -- last frame of the clip
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject.
///   - Assign groundSlamVFXPrefab (the GroundSlamVFX prefab).
///   - Assign playerLayer.
///   - slamRadius should match GroundSlamVFX.maxScale / 2.
/// </summary>
public class GroundSlamAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Ground Slam Settings")]
    [Tooltip("Damage dealt to the player if within slamRadius.")]
    public float damage = 45f;

    [Tooltip("Radius of the AOE damage and shockwave ring.")]
    public float slamRadius = 5f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Header("VFX")]
    [Tooltip("GroundSlamVFX prefab -- spawned at the boss's feet on impact.")]
    public GameObject groundSlamVFXPrefab;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerGroundSlam = Animator.StringToHash("triggerGroundSlam");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError("[GroundSlamAttack] No Animator found on this GameObject.");
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerGroundSlam);
        Debug.Log("[GroundSlamAttack] Ground Slam triggered.");
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    public override void OnHitboxOpen()
    {
        if (!IsActive) return;

        Vector3 slamPoint = new Vector3(transform.position.x, transform.position.y, transform.position.z);

        // Spawn VFX at feet.
        if (groundSlamVFXPrefab != null)
            Instantiate(groundSlamVFXPrefab, slamPoint, Quaternion.identity);

        // AOE damage check.
        var hits = Physics.OverlapSphere(slamPoint, slamRadius, playerLayer,
                                          QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.ClosestPoint(slamPoint));
                Debug.Log($"[GroundSlamAttack] Hit {hit.gameObject.name} for {damage} damage.");
            }
        }
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
