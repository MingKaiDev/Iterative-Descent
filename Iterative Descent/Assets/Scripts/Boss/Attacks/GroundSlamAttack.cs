using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// DROID-7 Pounce Slam -- boss leaps toward the player's position at the moment
/// the attack begins, then slams down for AOE damage on landing.
///
/// Movement model:
///   PerformAttack  -- captures player XZ, disconnects NavMeshAgent, starts LeapRoutine.
///   LeapRoutine    -- moves boss along an arc toward _targetPosition over leapDuration.
///                     Runs until OnHitboxOpen sets _hasLanded = true (or leapDuration elapses).
///   OnHitboxOpen   -- snaps boss to target, deals AOE damage, spawns VFX.
///   OnAttackAnimEnd-- re-enables NavMeshAgent, calls EndAttack().
///
/// Tuning note:
///   Set leapDuration to match the animation's air-time so the boss reaches the
///   target just as OnHitboxOpen fires. Err slightly short -- the snap in
///   OnHitboxOpen covers the last few frames.
///
/// Animation Event setup (triggerGroundSlam clip):
///   - "OnHitboxOpen"    -- frame where feet hit the ground
///   - "OnAttackAnimEnd" -- last frame of the clip
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject.
///   - Assign groundSlamVFXPrefab.
///   - Assign playerLayer.
///   - slamRadius should match GroundSlamVFX.maxScale / 2.
/// </summary>
public class GroundSlamAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Leap Movement")]
    [Tooltip("How long (seconds) the boss spends in the air. Tune to match the animation air-time.")]
    public float leapDuration = 0.6f;

    [Tooltip("Peak height of the arc above the start/end Y.")]
    public float leapHeight = 3f;

    [Header("Ground Slam Settings")]
    [Tooltip("Damage dealt to the player if within slamRadius on landing.")]
    public float damage = 45f;

    [Tooltip("Radius of the AOE damage and shockwave ring.")]
    public float slamRadius = 5f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Header("VFX")]
    [Tooltip("GroundSlamVFX prefab -- spawned at the boss's feet on landing.")]
    public GameObject groundSlamVFXPrefab;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerGroundSlam = Animator.StringToHash("triggerGroundSlam");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator     _animator;
    private NavMeshAgent _agent;
    private Transform    _player;

    // ─── Private State ───────────────────────────────────────────────────────────
    private Vector3 _targetPosition;
    private bool    _hasLanded;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _agent    = GetComponent<NavMeshAgent>();

        var ph = FindObjectOfType<PlayerHealth>();
        if (ph != null) _player = ph.transform;

        if (_animator == null) Debug.LogError("[GroundSlamAttack] No Animator found.");
        if (_agent    == null) Debug.LogError("[GroundSlamAttack] No NavMeshAgent found.");
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null || _player == null) return;

        // Capture player XZ at the moment the attack starts.
        _targetPosition = new Vector3(_player.position.x, transform.position.y, _player.position.z);
        _hasLanded      = false;

        // Disconnect NavMeshAgent so we can drive position manually.
        if (_agent != null)
        {
            _agent.updatePosition = false;
            _agent.updateRotation = false;
        }

        _animator.SetTrigger(TriggerGroundSlam);
        StartCoroutine(LeapRoutine());
        Debug.Log($"[GroundSlamAttack] Pouncing toward {_targetPosition}.");
    }

    // ─── Leap Coroutine ──────────────────────────────────────────────────────────

    IEnumerator LeapRoutine()
    {
        Vector3 startPos = transform.position;

        // Face target immediately.
        Vector3 dir = _targetPosition - startPos;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        float elapsed = 0f;

        while (elapsed < leapDuration && !_hasLanded)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / leapDuration);

            float x    = Mathf.Lerp(startPos.x, _targetPosition.x, t);
            float z    = Mathf.Lerp(startPos.z, _targetPosition.z, t);
            float arcY = startPos.y + Mathf.Sin(t * Mathf.PI) * leapHeight;

            transform.position = new Vector3(x, arcY, z);

            if (_agent != null)
                _agent.nextPosition = transform.position;

            yield return null;
        }
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    public override void OnHitboxOpen()
    {
        if (!IsActive) return;
        PlaySound(hitboxOpenClip);

        _hasLanded = true; // stops LeapRoutine if still running

        // Snap to target XZ so damage origin is accurate.
        transform.position = _targetPosition;
        if (_agent != null)
            _agent.Warp(_targetPosition);

        Vector3 slamPoint = transform.position;

        // Spawn VFX at feet.
        if (groundSlamVFXPrefab != null)
            Instantiate(groundSlamVFXPrefab, slamPoint, Quaternion.identity);

        // AOE damage -- only hits player if they stayed near the target position.
        var hits = Physics.OverlapSphere(slamPoint, slamRadius, playerLayer,
                                          QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.ClosestPoint(slamPoint));
                Debug.Log($"[GroundSlamAttack] Slam hit {hit.gameObject.name} for {damage} damage.");
            }
        }
    }

    public override void OnAttackAnimEnd()
    {
        // Re-enable NavMeshAgent before ending so the state machine can resume pathing.
        if (_agent != null)
        {
            _agent.updatePosition = true;
            _agent.updateRotation = true;
        }

        EndAttack();
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
