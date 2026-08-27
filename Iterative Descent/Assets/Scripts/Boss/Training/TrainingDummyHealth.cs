using System;
using UnityEngine;

/// <summary>
/// Health component for scripted training opponents (PlayerAgentAggressive/Circler/Kiter)
/// fighting DROID-7 inside Training.unity. Implements IDamageable so boss attacks damage
/// it exactly like they damage the real PlayerHealth -- no attack script needed any changes.
///
/// Unlike PlayerHealth/BossHealth (both singletons in practice), multiple TrainingDummyHealth
/// instances can exist at once (one per opponent slot in the Agents parent), so the static
/// events pass "this" as a sender so listeners can tell which dummy changed.
///
/// Subscribe from BossTrainingEnv (Step 4) or a training HUD:
///   TrainingDummyHealth.OnHealthChanged += (dummy, current, max) => { ... };
///   TrainingDummyHealth.OnDummyDied     += (dummy)               => { ... };
/// </summary>
public class TrainingDummyHealth : MonoBehaviour, IDamageable
{
    // ─── Events ─────────────────────────────────────────────────────────────────
    public static event Action<TrainingDummyHealth, float, float> OnHealthChanged; // (dummy, current, max)
    public static event Action<TrainingDummyHealth>               OnDummyDied;

    // ─── Inspector ──────────────────────────────────────────────────────────────
    [Header("Health")]
    public float maxHealth = 100f;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public float CurrentHealth => _currentHealth;
    public bool  IsAlive       => _currentHealth > 0f;

    // ─── Private ────────────────────────────────────────────────────────────────
    private float _currentHealth;
    private bool  _isDead;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _currentHealth = maxHealth;
        _isDead        = false;
    }

    void Start()
    {
        OnHealthChanged?.Invoke(this, _currentHealth, maxHealth);
    }

    // ─── IDamageable ────────────────────────────────────────────────────────────

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        OnHealthChanged?.Invoke(this, _currentHealth, maxHealth);

        if (_currentHealth <= 0f)
            Die();
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Directly reset HP. Called by BossTrainingEnv (Step 4) between episodes.</summary>
    public void ResetHealth()
    {
        _currentHealth = maxHealth;
        _isDead        = false;
        OnHealthChanged?.Invoke(this, _currentHealth, maxHealth);
    }

    // ─── Internal ───────────────────────────────────────────────────────────────

    void Die()
    {
        if (_isDead) return;
        _isDead = true;
        OnDummyDied?.Invoke(this);
        Debug.Log($"[TrainingDummyHealth] {name} died.");
    }
}
