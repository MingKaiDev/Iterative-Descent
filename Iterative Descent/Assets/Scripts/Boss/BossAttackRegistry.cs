using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all boss attacks and decides when to fire one.
///
/// In Combat state, polls every attackDecisionInterval seconds.
/// Picks a random attack that is not on cooldown and executes it.
/// Calls BossStateMachine.EnterAttacking() before the attack and
/// BossStateMachine.ExitAttacking() when it ends.
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject alongside BossStateMachine.
///   - Add concrete attack components (SlashAttack, BladeSweep, etc.) to the same GameObject.
///   - This script finds them automatically via GetComponents<BossAttackBase>().
/// </summary>
[RequireComponent(typeof(BossStateMachine))]
public class BossAttackRegistry : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Attack Timing")]
    [Tooltip("How many seconds between attack decisions while in Combat state.")]
    public float attackDecisionInterval = 2f;

    // ─── Private ────────────────────────────────────────────────────────────────
    private BossAttackBase[]  _attacks;
    private BossStateMachine  _stateMachine;
    private Transform         _player;
    private float             _decisionTimer;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _stateMachine = GetComponent<BossStateMachine>();
        _attacks      = GetComponents<BossAttackBase>();

        foreach (var attack in _attacks)
            attack.OnAttackEnded += HandleAttackEnded;

        if (_attacks.Length == 0)
            Debug.LogWarning("[BossAttackRegistry] No BossAttackBase components found on this GameObject.");

        var playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
            _player = playerHealth.transform;
        else
            Debug.LogWarning("[BossAttackRegistry] No PlayerHealth found in scene.");
    }

    void OnDestroy()
    {
        foreach (var attack in _attacks)
            attack.OnAttackEnded -= HandleAttackEnded;
    }

    void Update()
    {
        // Only make attack decisions during Combat state.
        if (_stateMachine.CurrentState != BossStateMachine.BossState.Combat) return;

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

        // Collect attacks that are off cooldown AND within range.
        var available = new List<BossAttackBase>();
        foreach (var attack in _attacks)
        {
            if (!attack.IsOnCooldown && distToPlayer <= attack.maxRange)
                available.Add(attack);
        }

        if (available.Count == 0) return;

        var chosen = available[UnityEngine.Random.Range(0, available.Count)];
        _stateMachine.EnterAttacking();
        chosen.Execute();
    }

    // ─── Public API (RL/training only, called by BossTrainingEnv) ───────────────

    /// <summary>Overrides the auto-found FindFirstObjectByType&lt;PlayerHealth&gt;() target.
    /// Mirrors BossStateMachine.SetTarget -- kept independent since this class caches its
    /// own _player reference separately. Never called in the live game.</summary>
    public void SetTarget(Transform target) => _player = target;

    void HandleAttackEnded()
    {
        // Only exit Attacking state if we are actually in it
        // (guard against stale callbacks during phase transitions or death).
        if (_stateMachine.CurrentState == BossStateMachine.BossState.Attacking)
            _stateMachine.ExitAttacking();
    }
}
