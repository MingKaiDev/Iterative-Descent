using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// DROID-7 Teleport Pounce -- boss leaps on the spot, vanishes at the arc peak,
/// and reappears directly behind the player.
///
/// Flow:
///   PerformAttack   -- triggers pounce animation, starts TeleportRoutine.
///   TeleportRoutine -- waits for arc peak (jumpDuration * teleportAtPeak),
///                      spawns TeleportOutVFX, hides mesh, warps behind player,
///                      waits teleportDelay, spawns TeleportInVFX, shows mesh.
///   OnHitboxOpen    -- AOE damage at landing position.
///   OnAttackAnimEnd -- safety re-enable renderers, EndAttack().
///
/// Tuning:
///   jumpDuration   = total animation air-time in seconds.
///   teleportAtPeak = fraction (0-1) of jumpDuration when the warp fires.
///                    0.5 fires at the midpoint; push toward 0.4 if the animation
///                    peaks early.
///
/// Animation Event setup (triggerPounce clip):
///   - "OnHitboxOpen"    -- frame where feet land (AOE damage)
///   - "OnAttackAnimEnd" -- last frame of the clip
///
/// Unity Setup:
///   - Attach to DROID-7 root GameObject.
///   - Assign teleportOutVFX, teleportInVFX (prefabs in Assets/Art/VFX/Boss/).
///   - Assign playerLayer.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PounceAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Teleport Timing")]
    [Tooltip("Total animation air-time in seconds. Tune to match the pounce clip.")]
    public float jumpDuration = 0.8f;

    [Tooltip("Fraction of jumpDuration (0-1) at which the teleport fires. 0.5 = arc peak.")]
    [Range(0.1f, 0.9f)]
    public float teleportAtPeak = 0.5f;

    [Tooltip("Seconds the boss stays invisible between TeleportOut and TeleportIn.")]
    public float teleportDelay = 0.25f;

    [Header("Arrival Position")]
    [Tooltip("How far behind the player (in the player's -forward direction) to land.")]
    public float behindPlayerOffset = 1.5f;

    [Tooltip("Search radius passed to NavMesh.SamplePosition when finding the arrival point.")]
    public float navMeshSampleRadius = 5f;

    [Header("On Landing")]
    [Tooltip("Damage dealt if player is within hitRadius on landing.")]
    public float damage = 30f;

    [Tooltip("Radius of the landing AOE.")]
    public float hitRadius = 2.5f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Header("VFX")]
    [Tooltip("Particle prefab spawned at departure (boss vanishes).")]
    public GameObject teleportOutVFX;

    [Tooltip("Particle prefab spawned at arrival (boss appears).")]
    public GameObject teleportInVFX;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerPounce = Animator.StringToHash("triggerPounce");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator               _animator;
    private NavMeshAgent           _agent;
    private Transform              _player;
    private SkinnedMeshRenderer[]  _renderers;

    void Start()
    {
        _animator  = GetComponent<Animator>();
        _agent     = GetComponent<NavMeshAgent>();
        _renderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null) _player = ph.transform;

        if (_animator  == null) Debug.LogError("[PounceAttack] No Animator found.");
        if (_agent     == null) Debug.LogError("[PounceAttack] No NavMeshAgent found.");
        if (_renderers.Length == 0) Debug.LogWarning("[PounceAttack] No SkinnedMeshRenderers found -- invisible effect will not work.");
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null || _player == null) return;
        _animator.SetTrigger(TriggerPounce);
        StartCoroutine(TeleportRoutine());
        Debug.Log("[PounceAttack] Pounce started.");
    }

    // ─── Teleport Coroutine ──────────────────────────────────────────────────────

    IEnumerator TeleportRoutine()
    {
        // Wait for the arc peak before vanishing.
        yield return new WaitForSeconds(jumpDuration * teleportAtPeak);

        // --- DISAPPEAR ---
        if (teleportOutVFX != null)
            Instantiate(teleportOutVFX, transform.position, Quaternion.identity);
        SetRenderersEnabled(false);

        // Compute arrival point: behind the player on the NavMesh.
        Vector3 behindPlayer = _player.position - _player.forward * behindPlayerOffset;

        Vector3 arrivalPos;
        if (NavMesh.SamplePosition(behindPlayer, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            arrivalPos = hit.position;
        }
        else if (NavMesh.SamplePosition(_player.position, out NavMeshHit fallback, navMeshSampleRadius, NavMesh.AllAreas))
        {
            // Fallback: land at player's feet if behind-point fails.
            arrivalPos = fallback.position;
            Debug.LogWarning("[PounceAttack] Behind-player NavMesh sample failed -- falling back to player position.");
        }
        else
        {
            // Cannot find any NavMesh point; abort teleport, re-enable and end attack.
            Debug.LogError("[PounceAttack] NavMesh.SamplePosition failed entirely. Aborting teleport.");
            SetRenderersEnabled(true);
            EndAttack();
            yield break;
        }

        // Brief invisible "in transit" window.
        yield return new WaitForSeconds(teleportDelay);

        // --- APPEAR ---
        _agent.Warp(arrivalPos);

        // Face the player on arrival so the landing animation reads correctly.
        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(toPlayer);

        if (teleportInVFX != null)
            Instantiate(teleportInVFX, arrivalPos, Quaternion.identity);
        SetRenderersEnabled(true);

        Debug.Log($"[PounceAttack] Teleported to {arrivalPos}.");
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    public override void OnHitboxOpen()
    {
        if (!IsActive) return;
        PlaySound(hitboxOpenClip);

        var hits = Physics.OverlapSphere(transform.position, hitRadius, playerLayer,
                                          QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.ClosestPoint(transform.position));
                Debug.Log($"[PounceAttack] Landing hit {hit.gameObject.name} for {damage} damage.");
            }
        }
    }

    public override void OnAttackAnimEnd()
    {
        // Safety: guarantee the boss is visible even if the coroutine was interrupted.
        SetRenderersEnabled(true);
        EndAttack();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    void SetRenderersEnabled(bool state)
    {
        foreach (var r in _renderers)
            r.enabled = state;
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
