using System;
using UnityEngine;

/// <summary>
/// Boss HP component for DROID-7.
/// Implements IDamageable so PlayerCombat can call TakeDamage() without a direct reference.
///
/// Static events (subscribe from BossHUD, BossStateMachine, ARBITEXCommentator, etc.):
///   BossHealth.OnHealthChanged += (current, max) => { ... };
///   BossHealth.OnPhaseTwo      += ()             => { ... };   // fires once at phaseThreshold HP
///   BossHealth.OnBossDeath     += ()             => { ... };
/// </summary>
public class BossHealth : MonoBehaviour, IDamageable
{
    // ─── Events ─────────────────────────────────────────────────────────────────
    public static event Action<float, float> OnHealthChanged;  // (current, max)
    public static event Action               OnPhaseTwo;
    public static event Action               OnBossDeath;
    public static event Action               OnHealthReset;   // fired by ResetHealth() -- see below

    // ─── Inspector ──────────────────────────────────────────────────────────────
    [Header("Health")]
    [Tooltip("Total HP for the boss.")]
    public float maxHealth = 500f;

    [Header("Phase 2")]
    [Tooltip("HP fraction at which Phase 2 begins (0.5 = 50%).")]
    [Range(0.01f, 0.99f)]
    public float phaseThreshold = 0.5f;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public float CurrentHealth  => _currentHealth;
    public bool  IsAlive        => _currentHealth > 0f;
    public bool  IsPhaseTwo     => _isPhaseTwo;

    // ─── Private ────────────────────────────────────────────────────────────────
    private float _currentHealth;
    private bool  _isDead;
    private bool  _isPhaseTwo;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _currentHealth = maxHealth;
        _isDead        = false;
        _isPhaseTwo    = false;
    }

    void Start()
    {
        // Push initial value so BossHUD fills correctly on scene load.
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    // ─── IDamageable ────────────────────────────────────────────────────────────

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        CheckPhaseTransition();

        if (_currentHealth <= 0f)
            Die();
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Directly set current HP. Originally training-only (BossTrainingEnv resetting
    /// episodes); also called now by BossStateMachine.ResetForCheckpointRespawn() for a
    /// live-game checkpoint respawn while the boss is still alive. Does NOT fire
    /// OnPhaseTwo/OnBossDeath -- those are one-shot story beats, not something a reset
    /// should replay. Fires OnHealthReset (in addition to OnHealthChanged) so listeners
    /// like BossHUD can undo any one-shot visual state OnPhaseTwo left behind (e.g. the
    /// health bar's red phase-2 tint).
    /// </summary>
    public void ResetHealth()
    {
        _currentHealth = maxHealth;
        _isDead        = false;
        _isPhaseTwo    = false;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnHealthReset?.Invoke();
    }

    // ─── Debug / Editor Helpers ─────────────────────────────────────────────────

    [ContextMenu("Test: Deal 251 Damage (trigger Phase 2)")]
    void Debug_DealPhase2Damage() => TakeDamage(251f, Vector3.zero);

    [ContextMenu("Test: Deal 500 Damage (kill boss)")]
    void Debug_KillBoss() => TakeDamage(500f, Vector3.zero);

    [ContextMenu("Test: Reset Health")]
    void Debug_Reset() => ResetHealth();

    // ─── Internal ───────────────────────────────────────────────────────────────

    void CheckPhaseTransition()
    {
        if (_isPhaseTwo) return;
        if (_currentHealth / maxHealth <= phaseThreshold)
        {
            _isPhaseTwo = true;
            OnPhaseTwo?.Invoke();
            Debug.Log("[BossHealth] Phase 2 triggered.");
        }
    }

    void Die()
    {
        if (_isDead) return;
        _isDead = true;

        OnBossDeath?.Invoke();
        Debug.Log("[BossHealth] Boss died.");
    }
}
