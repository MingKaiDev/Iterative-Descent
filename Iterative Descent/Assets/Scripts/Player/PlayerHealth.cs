using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player health component. Implements IDamageable so enemies and hazards
/// can call TakeDamage() without needing a direct reference to this class.
///
/// Also owns the medkit inventory (2026-09 revamp): health kits picked up in
/// the world no longer heal automatically -- they add to a capped medkit
/// count, and the player presses Q to consume one, healing a flat fraction
/// of max health. See AddMedkit() (called by HealthPickup) and UseMedkit()
/// (polled from Update()).
///
/// Subscribe to the static events from CombatHUD or any other listener:
///   PlayerHealth.OnHealthChanged      += (current, max) => { ... };
///   PlayerHealth.OnMedkitCountChanged += (current, max) => { ... };
///   PlayerHealth.OnPlayerDied         += () => { ... };
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    // ─── Events ─────────────────────────────────────────────────────────────────
    public static event Action<float, float> OnHealthChanged;       // (current, max)
    public static event Action<int, int>      OnMedkitCountChanged; // (current, max)
    public static event Action               OnPlayerDied;

    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Medkits (Q to use)")]
    [Tooltip("Maximum medkits the player can carry at once. Health pickups beyond " +
             "this cap are declined (left in the world) rather than wasted.")]
    public int maxMedkits = 3;
    [Range(0f, 1f)]
    [Tooltip("Fraction of max health restored per medkit use, e.g. 0.3 = 30% of max HP.")]
    public float medkitHealFraction = 0.30f;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public float CurrentHealth => _currentHealth;
    public bool  IsAlive       => _currentHealth > 0f;
    public int   MedkitCount   => _medkitCount;

    // ─── Private ────────────────────────────────────────────────────────────────
    private float _currentHealth;
    private int   _medkitCount;
    private bool  _isDead;
    private PlayerAudioController _audio;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _currentHealth = maxHealth;
        _audio = GetComponent<PlayerAudioController>();
    }

    void Start()
    {
        // Push initial values to HUD on scene start.
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnMedkitCountChanged?.Invoke(_medkitCount, maxMedkits);
    }

    void Update()
    {
        if (_isDead) return;
        if (Keyboard.current.qKey.wasPressedThisFrame)
            UseMedkit();
    }

    // ─── IDamageable ────────────────────────────────────────────────────────────

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        if (_currentHealth <= 0f)
            Die();
        else
            _audio?.PlayHurt();
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Call from herbs / first-aid item pickups, or from UseMedkit() below.</summary>
    public void Heal(float amount)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>
    /// Called by HealthPickup when the player walks over a health kit in the world.
    /// Returns false (adds nothing) if the player is already carrying the max --
    /// the pickup script uses this to decide whether to actually consume itself,
    /// so a declined pickup stays in the world for later.
    /// </summary>
    public bool AddMedkit()
    {
        if (_isDead || _medkitCount >= maxMedkits) return false;

        _medkitCount++;
        OnMedkitCountChanged?.Invoke(_medkitCount, maxMedkits);
        return true;
    }

    /// <summary>
    /// Consumes one held medkit and heals medkitHealFraction of max health.
    /// No-ops (and does not consume a medkit) if the player has none, or if
    /// already at full health -- prevents wasting a scarce medkit for zero gain.
    /// Polled from Update() on the Q key; also safe to call directly (e.g. from a
    /// future UI "use" button).
    /// </summary>
    public void UseMedkit()
    {
        if (_isDead || _medkitCount <= 0) return;
        if (_currentHealth >= maxHealth) return;

        _medkitCount--;
        Heal(maxHealth * medkitHealFraction);
        OnMedkitCountChanged?.Invoke(_medkitCount, maxMedkits);
        Debug.Log($"[PlayerHealth] Used medkit. +{maxHealth * medkitHealFraction:F0} HP. " +
                  $"{_medkitCount}/{maxMedkits} medkits remaining.");
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
