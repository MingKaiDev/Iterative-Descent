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
        // Collect attacks that are off cooldown.
        var available = new List<BossAttackBase>();
        foreach (var attack in _attacks)
        {
            if (!attack.IsOnCooldown)
                available.Add(attack);
        }

        if (available.Count == 0) return;

        var chosen = available[UnityEngine.Random.Range(0, available.Count)];
        _stateMachine.EnterAttacking();
        chosen.Execute();
    }

    void HandleAttackEnded()
    {
        // Only exit Attacking state if we are actually in it
        // (guard against stale callbacks during phase transitions or death).
        if (_stateMachine.CurrentState == BossStateMachine.BossState.Attacking)
            _stateMachine.ExitAttacking();
    }
}
