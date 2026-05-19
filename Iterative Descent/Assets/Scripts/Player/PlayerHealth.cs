using System;
using UnityEngine;

/// <summary>
/// Player health component. Implements IDamageable so enemies and hazards
/// can call TakeDamage() without needing a direct reference to this class.
///
/// Subscribe to the static events from CombatHUD or any other listener:
///   PlayerHealth.OnHealthChanged += (current, max) => { ... };
///   PlayerHealth.OnPlayerDied    += () => { ... };
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    // ─── Events ─────────────────────────────────────────────────────────────────
    public static event Action<float, float> OnHealthChanged;  // (current, max)
    public static event Action               OnPlayerDied;

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
    }

    void Start()
    {
        // Push initial value to HUD on scene start.
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    // ─── IDamageable ────────────────────────────────────────────────────────────

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        if (_currentHealth <= 0f)
            Die();
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Call from herbs / first-aid item pickups.</summary>
    public void Heal(float amount)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    // ─── Internal ───────────────────────────────────────────────────────────────

    void Die()
    {
        if (_isDead) return;
        _isDead = true;

        OnPlayerDied?.Invoke();
        Debug.Log("[PlayerHealth] Player died.");

        // TODO: trigger death animation, disable movement, show game-over screen
    }
}
