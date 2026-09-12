using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all Deimos attacks and decides when to fire one. Mirrors BossAttackRegistry.cs
/// (ARES/DROID-7) exactly, retargeted at DeimosStateMachine/DeimosAttackBase -- kept as its own
/// class for the same reason DeimosStateMachine/DeimosHealth are separate from their ARES
/// counterparts (one dedicated script per boss).
///
/// In Combat state, polls every attackDecisionInterval seconds. Picks a random attack that is
/// not on cooldown and within [minRange, maxRange] of the player, and executes it. Calls
/// DeimosStateMachine.EnterAttacking() before the attack and DeimosStateMachine.ExitAttacking()
/// when it ends. minRange (added alongside JumpForwardAttack, Move 6) defaults to 0 on every
/// attack that doesn't set it, so this range check is unchanged for MMAKick/Dropkick/Flying Knee/
/// Superhuman Choke Lift -- only Jump Forward actually uses a nonzero minRange, to keep it out of
/// the pool until the player is too far for anything else to reach.
///
/// Note: JumpBackwardAttack (Move 7) can also trigger ITSELF outside this normal poll entirely,
/// via its own encirclement watchdog calling EnterAttacking()/Execute() directly when the player
/// circles behind Deimos -- see that class's doc comment. This registry doesn't need to know
/// about that; CurrentState == Attacking already blocks TryExecuteRandomAttack() for the
/// duration regardless of which caller started the attack, and HandleAttackEnded() below still
/// fires ExitAttacking() the same way once it's done.
///
/// One extra guard beyond ARES's version: attack decisions are also gated on
/// DeimosStateMachine.IsLocomotionReady, so Deimos can't kick mid-Sit-To-Stand just because
/// CurrentState already flipped to Combat the instant the fight started (see
/// DeimosStateMachine.UpdateCombat()'s own use of the same flag for the movement-gating fix).
///
/// Unity Setup:
///   - Attach to the Deimos root GameObject alongside DeimosStateMachine.
///   - Add concrete attack components (MMAKickAttack, and future Dropkick/JumpBack/etc.) to the
///     same GameObject. This script finds them automatically via GetComponents&lt;DeimosAttackBase&gt;().
/// </summary>
[RequireComponent(typeof(DeimosStateMachine))]
public class DeimosAttackRegistry : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Attack Timing")]
    [Tooltip("How many seconds between attack decisions while in Combat state.")]
    public float attackDecisionInterval = 2f;

    // ─── Private ────────────────────────────────────────────────────────────────
    private DeimosAttackBase[]  _attacks;
    private DeimosStateMachine  _stateMachine;
    private Transform           _player;
    private float               _decisionTimer;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _stateMachine = GetComponent<DeimosStateMachine>();
        _attacks      = GetComponents<DeimosAttackBase>();

        foreach (var attack in _attacks)
            attack.OnAttackEnded += HandleAttackEnded;

        if (_attacks.Length == 0)
            Debug.LogWarning("[DeimosAttackRegistry] No DeimosAttackBase components found on this GameObject.");

        var playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
            _player = playerHealth.transform;
        else
            Debug.LogWarning("[DeimosAttackRegistry] No PlayerHealth found in scene.");
    }

    void OnDestroy()
    {
        foreach (var attack in _attacks)
            attack.OnAttackEnded -= HandleAttackEnded;
    }

    void Update()
    {
        // Only make attack decisions during Combat state, and only once Deimos has actually
        // finished standing up (see class doc comment).
        if (_stateMachine.CurrentState != DeimosStateMachine.DeimosState.Combat) return;
        if (!_stateMachine.IsLocomotionReady) return;

        _decisionTimer -= Time.deltaTime;
        if (_decisionTimer <= 0f)
        {
            _decisionTimer = attackDecisionInterval;
            TryExecuteRandomAttack();
        }
    }

    // ─── Internal ────────────────────────────────────────────────────────────────

    void TryExecuteRandomAttack()
    {
        if (_player == null) return;

        float distToPlayer = Vector3.Distance(transform.position, _player.position);

        // Collect attacks that are off cooldown AND within [minRange, maxRange].
        var available = new List<DeimosAttackBase>();
        foreach (var attack in _attacks)
        {
            if (!attack.IsOnCooldown && distToPlayer >= attack.minRange && distToPlayer <= attack.maxRange)
                available.Add(attack);
        }

        if (available.Count == 0) return;

        var chosen = available[UnityEngine.Random.Range(0, available.Count)];
        _stateMachine.EnterAttacking();
        chosen.Execute();
    }

    // ─── Public API (RL/training only) ───────────────────────────────────────────

    /// <summary>Overrides the auto-found FindFirstObjectByType&lt;PlayerHealth&gt;() target.
    /// Mirrors BossAttackRegistry.SetTarget -- kept independent since this class caches its
    /// own _player reference separately. Never called in the live game.</summary>
    public void SetTarget(Transform target) => _player = target;

    void HandleAttackEnded()
    {
        // Only exit Attacking state if we are actually in it
        // (guard against stale callbacks during future phase transitions or death).
        if (_stateMachine.CurrentState == DeimosStateMachine.DeimosState.Attacking)
            _stateMachine.ExitAttacking();
    }
}
