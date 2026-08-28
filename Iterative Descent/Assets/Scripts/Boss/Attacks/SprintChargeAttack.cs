using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// DROID-7 Sprint Charge attack -- boss charges in a straight line toward the player.
/// Loops the SprintCharge animation clip until stopped.
///
/// Stop conditions (whichever comes first):
///   1. Player hit -- deals damage and ends charge
///   2. Wall hit   -- SphereCast detects obstacle ahead
///   3. Timeout    -- chargeDuration seconds elapsed
///
/// Animator Setup:
///   - SprintCharge clip must be set to LOOP in the Animator.
///   - Add bool "isCharging" to DROIDAnimator (already done).
///   - Transition in:  Any State -> SprintCharge on triggerSprintCharge
///   - Transition out: SprintCharge -> Combat idle when isCharging = false
///     (set "Has Exit Time" OFF, "Transition Duration" to 0.1)
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject (same as NavMeshAgent and Animator).
///   - Assign playerLayer in the Inspector.
///   - "Level 1 Obstacle" layer is used automatically for wall detection.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SprintChargeAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Sprint Charge Settings")]
    [Tooltip("Damage dealt to the player on contact.")]
    public float damage = 50f;

    [Tooltip("Movement speed during the charge.")]
    public float chargeSpeed = 12f;

    [Tooltip("Maximum duration of the charge in seconds before it stops automatically.")]
    public float chargeDuration = 2f;

    [Tooltip("Radius of the player hit detection sphere during the charge.")]
    public float playerHitRadius = 1.2f;

    [Tooltip("Radius used for wall SphereCast ahead of the boss.")]
    public float wallCheckRadius = 0.5f;

    [Tooltip("How far ahead to check for walls each frame.")]
    public float wallCheckDistance = 1f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    // ─── Animator Parameters ─────────────────────────────────────────────────────
    private static readonly int TriggerSprintCharge = Animator.StringToHash("triggerSprintCharge");
    private static readonly int isChargingHash      = Animator.StringToHash("isCharging");

    // ─── Component References ────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Animator     _animator;
    private Transform    _player;

    // ─── Private ────────────────────────────────────────────────────────────────
    private int _wallLayer;
    private Coroutine _chargeRoutine;

    void Start()
    {
        _agent    = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _wallLayer = LayerMask.GetMask("Level 1 Obstacle");

        var playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
            _player = playerHealth.transform;

        if (_animator == null) Debug.LogError("[SprintChargeAttack] No Animator found.");
        if (_agent == null)    Debug.LogError("[SprintChargeAttack] No NavMeshAgent found.");
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null || _agent == null) return;

        // Lock charge direction toward player at the moment the attack fires.
        Vector3 chargeDir = _player != null
            ? (new Vector3(_player.position.x, transform.position.y, _player.position.z) - transform.position).normalized
            : transform.forward;

        // Face the charge direction immediately.
        if (chargeDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(chargeDir);

        // Disconnect NavMeshAgent from driving position -- we move manually during charge.
        _agent.updatePosition = false;
        _agent.updateRotation = false;
        _agent.ResetPath();

        _animator.SetBool("isCharging", true);
        _animator.SetTrigger("triggerSprintCharge");

        _chargeRoutine = StartCoroutine(ChargeRoutine(chargeDir));
        Debug.Log("[SprintChargeAttack] Sprint Charge started.");
    }

    // ─── Charge Coroutine ────────────────────────────────────────────────────────

    IEnumerator ChargeRoutine(Vector3 chargeDir)
    {
        float elapsed   = 0f;
        bool  hitPlayer = false;

        while (elapsed < chargeDuration)
        {
            elapsed += Time.deltaTime;

            // Move forward manually.
            transform.position += chargeDir * chargeSpeed * Time.deltaTime;

            // Keep NavMeshAgent sim position in sync so re-enable doesn't snap.
            _agent.nextPosition = transform.position;

            // Check wall ahead.
            if (Physics.SphereCast(transform.position, wallCheckRadius, chargeDir,
                                   out RaycastHit _, wallCheckDistance, _wallLayer))
            {
                Debug.Log("[SprintChargeAttack] Hit wall -- charge stopped.");
                break;
            }

            // Check player hit (deal damage once, then stop).
            if (!hitPlayer)
            {
                var hits = Physics.OverlapSphere(transform.position, playerHitRadius, playerLayer);
                if (hits.Length > 0)
                {
                    hitPlayer = true;
                    PlaySound(hitboxOpenClip);
                    var damageable = hits[0].GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        var hitPoint = hits[0].ClosestPoint(transform.position);
                        damageable.TakeDamage(damage, hitPoint);
                        Debug.Log($"[SprintChargeAttack] Hit player for {damage} damage.");
                    }
                    break;
                }
            }

            yield return null;
        }

        StopCharge();
    }

    void StopCharge()
    {
        // Restore NavMeshAgent control.
        _agent.updatePosition = true;
        _agent.updateRotation = true;
        _agent.Warp(transform.position);

        _animator.SetBool("isCharging", false);

        Debug.Log("[SprintChargeAttack] Sprint Charge ended.");
        EndAttack();
    }

    // ─── Override to prevent accidental broadcast damage ─────────────────────────
    // Suppressed for damage reasons only -- hitboxOpenClip is played from
    // ChargeRoutine's hitPlayer branch above instead, at the actual contact frame.
    public override void OnHitboxOpen() { }

    // ─── RL-only target override ──────────────────────────────────────────────────

    /// <summary>Overrides the auto-found FindFirstObjectByType&lt;PlayerHealth&gt;() target.
    /// Added 2026-08-27: Training.unity's opponents use TrainingDummyHealth instead of
    /// PlayerHealth, so the automatic lookup in Start() always found nothing there, and every
    /// charge fell back to transform.forward -- the boss just charged in whatever direction it
    /// happened to be facing instead of toward the actual opponent. Called by BossTrainingEnv
    /// each episode, mirroring BossStateMachine.SetTarget()/BossAttackRegistry.SetTarget(). Never
    /// called in the live game.</summary>
    public void SetTarget(Transform target) => _player = target;

    // ─── RL-only cancellation override ────────────────────────────────────────────

    /// <summary>See BossAttackBase.CancelAttack() for the full story. If a charge is cut short
    /// mid-flight (StopAllCoroutines() in the base call above already killed ChargeRoutine),
    /// this restores the NavMeshAgent handoff StopCharge() would otherwise have done, so the
    /// agent isn't left permanently disconnected (updatePosition/updateRotation stuck false).
    /// Does NOT Warp() -- BossTrainingEnv.ResetEpisode() does that itself right after cancelling
    /// every attack, so this just needs to stop fighting that Warp, not pre-empt it.</summary>
    public override void CancelAttack()
    {
        bool wasActive = IsActive;
        base.CancelAttack();
        if (!wasActive) return;

        if (_agent != null)
        {
            _agent.updatePosition = true;
            _agent.updateRotation = true;
        }
        if (_animator != null)
            _animator.SetBool(isChargingHash, false);
    }
}
