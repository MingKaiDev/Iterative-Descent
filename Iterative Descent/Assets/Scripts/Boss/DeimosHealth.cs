using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss HP component for Deimos.
/// Deliberately a separate class from BossHealth.cs (ARES/DROID-7's health component) rather
/// than a shared one -- BossHealth's OnHealthChanged/OnPhaseTwo/OnBossDeath events are static,
/// so two bosses sharing that class would collide on the same event (e.g. BossHUD would react
/// to Deimos taking damage as if it were ARES). Same shape/contract as BossHealth otherwise.
///
/// Implements IDamageable so PlayerCombat can call TakeDamage() without a direct reference.
///
/// Static events (subscribe from a future DeimosHUD, DeimosStateMachine, etc.):
///   DeimosHealth.OnHealthChanged += (current, max) => { ... };
///   DeimosHealth.OnPhaseTwo      += ()             => { ... };   // fires once at phaseThreshold HP
///   DeimosHealth.OnBossDeath     += ()             => { ... };
///   DeimosHealth.OnStaggerStart  += ()             => { ... };   // "Hit To Body" flinch begins
///   DeimosHealth.OnStaggerEnd    += ()             => { ... };   // flinch recovers (skipped if Deimos died mid-stagger)
///
/// ─── Stagger (DPS Threshold) ───────────────────────────────────────────────────────────────
/// Ported from EnemyBase.cs's CheckStaggerOnHit/DecayStaggerResistance/StaggerRoutine (the
/// "flinch check" already used by every EnemyBase-derived enemy, e.g. EnemyBrute's Stagger
/// Animator trigger) -- same DPS-threshold-over-a-rolling-window math, same escalating
/// resistance-per-stagger-with-decay, same single-big-hit override, copied field-for-field
/// (tooltips included) rather than shared, for the same reason DeimosHealth itself is a
/// separate class from BossHealth: this project's convention is one dedicated script per
/// boss/enemy rather than a shared base that different callers could silently collide on
/// (EnemyBase's version is also an abstract-subclass hook shape -- OnStaggerStart()/
/// OnStaggerEnd() virtual methods overridden per-enemy -- which doesn't fit DeimosHealth at
/// all, since DeimosHealth has no subclass; this uses the same static-event "many independent
/// listeners self-subscribe" shape already established here for OnPhaseTwo/OnBossDeath/
/// DeimosStateMachine.OnCombatStart instead).
///
/// Unlike EnemyBase, this does NOT touch NavMeshAgent/Animator directly -- DeimosHealth has no
/// reference to either. DeimosStateMachine self-subscribes to OnStaggerStart/OnStaggerEnd and
/// owns all of that: cancelling any in-progress DeimosAttackBase, pausing the NavMeshAgent,
/// firing the "Hit" Animator trigger (which plays the Hit To Body clip, wired 2026-09-11
/// alongside this change), and resuming Combat once OnStaggerEnd fires. See
/// DeimosStateMachine.HandleStaggerStart()/HandleStaggerEnd() and its new Staggered state.
///
/// Also unlike EnemyBase, there is no EnemyDirector DDA damage-mitigation hook here --
/// EnemyDirector scales regular enemy encounters, not this boss fight, so TakeDamage() below
/// applies the incoming amount directly (matching DeimosHealth's existing pre-stagger
/// behaviour) rather than pulling in that scope.
/// </summary>
public class DeimosHealth : MonoBehaviour, IDamageable
{
    // ─── Events ─────────────────────────────────────────────────────────────────
    public static event Action<float, float> OnHealthChanged;  // (current, max)
    public static event Action               OnPhaseTwo;
    public static event Action               OnBossDeath;
    public static event Action               OnHealthReset;   // fired by ResetHealth() -- see below
    public static event Action               OnStaggerStart;  // "Hit To Body" flinch begins
    public static event Action               OnStaggerEnd;    // flinch recovers (not fired if Deimos died mid-stagger)

    // ─── Inspector ──────────────────────────────────────────────────────────────
    [Header("Health")]
    [Tooltip("Total HP for Deimos. Placeholder default -- not yet balanced.")]
    public float maxHealth = 500f;

    [Header("Phase 2")]
    [Tooltip("HP fraction at which Phase 2 begins (0.5 = 50%). Unused until a phase system is wired for Deimos.")]
    [Range(0.01f, 0.99f)]
    public float phaseThreshold = 0.5f;

    [Header("Stagger (DPS Threshold, ported from EnemyBase.cs -- see class doc comment)")]
    [Tooltip("If the damage taken within staggerWindowSeconds implies a damage-per-second " +
             "rate at or above this value, Deimos staggers: NavMesh movement/attacks pause and " +
             "OnStaggerStart fires. Placeholder default copied from EnemyBase -- tune against " +
             "Deimos's actual maxHealth and the player's real DPS once playable.")]
    public float staggerDpsThreshold = 40f;
    [Tooltip("Rolling time window (seconds) used to compute recent DPS for the stagger check. " +
             "A shotgun blast landing 3 pellets within this window sums their damage together.")]
    public float staggerWindowSeconds = 1f;
    [Tooltip("Seconds Deimos stays staggered before DeimosStateMachine resumes Combat.")]
    public float staggerDuration = 1f;
    [Tooltip("Minimum seconds between the END of one stagger and the next one being allowed " +
             "to trigger. Prevents a couple of stray hits right after recovering from " +
             "immediately re-triggering another stagger.")]
    public float staggerCooldown = 1.5f;
    [Tooltip("Each successive stagger during the same fight multiplies the effective " +
             "staggerDpsThreshold by this factor -- e.g. 1.5 means the 2nd stagger needs 1.5x " +
             "the DPS, the 3rd needs 2.25x, and so on, so a sustained weapon can't chain-" +
             "stunlock Deimos forever. Resets back to 1x via ResetHealth(), and also decays " +
             "gradually mid-fight -- see staggerResistanceDecayDelay/Rate below.")]
    public float staggerResistanceGrowth = 1.5f;
    [Tooltip("Seconds since the resistance multiplier last changed before it starts decaying " +
             "back toward 1x -- Deimos \"catching its breath.\" If the player keeps landing " +
             "hits inside this window the resistance stays at full strength (still can't be " +
             "chain-stunlocked), but a lull of this long lets a later burst stagger it again " +
             "without needing an ever-escalating amount of DPS.")]
    public float staggerResistanceDecayDelay = 4f;
    [Tooltip("Exponential decay rate (1/seconds) applied to the multiplier's excess above 1x " +
             "once staggerResistanceDecayDelay has elapsed with no change. Higher = faster " +
             "recovery. ~0.35 roughly halves the excess every 2 seconds.")]
    public float staggerResistanceDecayRate = 0.35f;
    [Tooltip("A SINGLE hit dealing at least this much raw damage instantly staggers, bypassing " +
             "the rolling-DPS window check entirely. Still scaled by " +
             "_staggerResistanceMultiplier like the windowed check. Leave very high (default) " +
             "to disable and rely purely on the windowed DPS check.")]
    public float staggerBigHitDamage = 99999f;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public float CurrentHealth  => _currentHealth;
    public bool  IsAlive        => _currentHealth > 0f;
    public bool  IsPhaseTwo     => _isPhaseTwo;

    /// <summary>True while a DPS-triggered "Hit To Body" flinch is in progress.</summary>
    public bool IsStaggered { get; private set; }

    // ─── Private ────────────────────────────────────────────────────────────────
    private float _currentHealth;
    private bool  _isDead;
    private bool  _isPhaseTwo;

    // ─── Stagger runtime state (mirrors EnemyBase.cs field-for-field) ───────────
    private readonly List<(float time, float amount)> _recentHits = new();
    private float     _staggerLockedUntil;   // Time.time value; no new stagger check while Time.time < this
    private Coroutine _staggerRoutine;
    private float     _staggerResistanceMultiplier = 1f;  // grows by staggerResistanceGrowth per stagger, resets on ResetHealth()
    private float     _lastResistanceChangeTime;           // Time.time the multiplier was last set/decayed -- anchors the decay calc

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _currentHealth = maxHealth;
        _isDead        = false;
        _isPhaseTwo    = false;
    }

    void Start()
    {
        // Push initial value so a future DeimosHUD fills correctly on scene load.
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    // ─── IDamageable ────────────────────────────────────────────────────────────

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);

        CheckPhaseTransition();
        // Same ordering as EnemyBase.TakeDamage(): the stagger check runs BEFORE the death
        // check below, so a killing blow can still start a stagger routine -- harmless, since
        // StaggerRoutine() bails out of firing OnStaggerEnd once _isDead is true (see below),
        // and Die()/DeimosStateMachine's own Dead-state handling already disables movement.
        CheckStaggerOnHit(amount);

        if (_currentHealth <= 0f)
            Die();
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Directly set current HP. Mirrors BossHealth.ResetHealth() -- available now for a future
    /// checkpoint-respawn hookup even though DeimosStateMachine doesn't call it yet. Does NOT
    /// fire OnPhaseTwo/OnBossDeath -- those are one-shot story beats, not something a reset
    /// should replay. Fires OnHealthReset (in addition to OnHealthChanged) so listeners can undo
    /// any one-shot visual state OnPhaseTwo left behind. Also clears stagger state -- a fresh
    /// fight (or a checkpoint retry) shouldn't carry over stagger resistance built up earlier,
    /// same reasoning as EnemyBase.Activate() resetting _staggerResistanceMultiplier.
    /// </summary>
    public void ResetHealth()
    {
        _currentHealth = maxHealth;
        _isDead        = false;
        _isPhaseTwo    = false;
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnHealthReset?.Invoke();

        if (_staggerRoutine != null)
        {
            StopCoroutine(_staggerRoutine);
            _staggerRoutine = null;
        }
        IsStaggered = false;
        _recentHits.Clear();
        _staggerLockedUntil = 0f;
        _staggerResistanceMultiplier = 1f;
        _lastResistanceChangeTime = Time.time;
    }

    // ─── Debug / Editor Helpers ─────────────────────────────────────────────────

    [ContextMenu("Test: Deal 251 Damage (trigger Phase 2)")]
    void Debug_DealPhase2Damage() => TakeDamage(251f, Vector3.zero);

    [ContextMenu("Test: Deal 500 Damage (kill boss)")]
    void Debug_KillBoss() => TakeDamage(500f, Vector3.zero);

    [ContextMenu("Test: Reset Health")]
    void Debug_Reset() => ResetHealth();

    /// <summary>Bypasses the DPS math and starts a stagger immediately -- for testing
    /// DeimosStateMachine's Hit reaction/Animator wiring without having to actually land
    /// enough hits in the Editor.</summary>
    [ContextMenu("Test: Force Stagger")]
    void Debug_ForceStagger()
    {
        if (_isDead) return;
        if (_staggerRoutine != null) StopCoroutine(_staggerRoutine);
        _staggerRoutine = StartCoroutine(StaggerRoutine());
    }

    // ─── Internal ───────────────────────────────────────────────────────────────

    void CheckPhaseTransition()
    {
        if (_isPhaseTwo) return;
        if (_currentHealth / maxHealth <= phaseThreshold)
        {
            _isPhaseTwo = true;
            OnPhaseTwo?.Invoke();
            Debug.Log("[DeimosHealth] Phase 2 threshold reached.");
        }
    }

    void Die()
    {
        if (_isDead) return;
        _isDead = true;

        OnBossDeath?.Invoke();
        Debug.Log("[DeimosHealth] Deimos died.");
    }

    // ─── Stagger (DPS Threshold) -- ported from EnemyBase.cs ────────────────────

    /// <summary>
    /// Records this hit's damage/timestamp, prunes anything older than
    /// staggerWindowSeconds, and triggers a stagger if EITHER the resulting rolling DPS
    /// meets staggerDpsThreshold, OR this single hit alone meets staggerBigHitDamage (see
    /// that field's tooltip -- lets a heavy single-shot weapon skip the windowed combo
    /// requirement). No-op while dead, already staggered, or still on staggerCooldown from
    /// a previous stagger. See EnemyBase.CheckStaggerOnHit() -- identical logic.
    /// </summary>
    void CheckStaggerOnHit(float amount)
    {
        if (_isDead || IsStaggered) return;
        if (Time.time < _staggerLockedUntil) return;

        DecayStaggerResistance();

        _recentHits.Add((Time.time, amount));
        _recentHits.RemoveAll(hit => Time.time - hit.time > staggerWindowSeconds);

        float windowDamage = 0f;
        foreach (var hit in _recentHits)
            windowDamage += hit.amount;

        float currentDps = windowDamage / staggerWindowSeconds;
        float effectiveThreshold = staggerDpsThreshold * _staggerResistanceMultiplier;
        float effectiveBigHit = staggerBigHitDamage * _staggerResistanceMultiplier;
        bool bigHitOverride = amount >= effectiveBigHit;
        if (!bigHitOverride && currentDps < effectiveThreshold) return;

        _recentHits.Clear();
        // Locked for the full stagger + cooldown so a fresh burst of hits landing while
        // already staggered can't queue up an immediate second stagger the instant this one ends.
        _staggerLockedUntil = Time.time + staggerDuration + staggerCooldown;

        // Next stagger needs proportionally more DPS -- diminishing returns so one weapon
        // can't permanently lock Deimos in a flinch loop. Fully resets in ResetHealth(); also
        // decays mid-fight -- see DecayStaggerResistance() -- so a long fight isn't limited to
        // a single stagger.
        _staggerResistanceMultiplier *= staggerResistanceGrowth;
        _lastResistanceChangeTime = Time.time;

        if (_staggerRoutine != null) StopCoroutine(_staggerRoutine);
        _staggerRoutine = StartCoroutine(StaggerRoutine());
    }

    /// <summary>
    /// Eases staggerResistanceMultiplier back toward 1x once staggerResistanceDecayDelay
    /// seconds have passed since it last changed. Called lazily from CheckStaggerOnHit
    /// (only when a new hit actually needs an up-to-date effective threshold) rather than
    /// every frame, so DeimosHealth needs no Update() polling for this.
    /// </summary>
    void DecayStaggerResistance()
    {
        if (_staggerResistanceMultiplier <= 1f) return;

        float elapsedSinceChange = Time.time - _lastResistanceChangeTime;
        if (elapsedSinceChange <= staggerResistanceDecayDelay) return;

        float decaySeconds = elapsedSinceChange - staggerResistanceDecayDelay;
        float decayFactor  = Mathf.Exp(-staggerResistanceDecayRate * decaySeconds);
        _staggerResistanceMultiplier = 1f + (_staggerResistanceMultiplier - 1f) * decayFactor;
        _lastResistanceChangeTime = Time.time; // restart the clock from now
    }

    /// <summary>
    /// Owns the stagger's lifetime only -- IsStaggered flag + the two static events.
    /// Deliberately does NOT touch NavMeshAgent/Animator (DeimosHealth has no reference to
    /// either) -- DeimosStateMachine.HandleStaggerStart()/HandleStaggerEnd() do that in
    /// response to OnStaggerStart/OnStaggerEnd, same "event fires, independent listener
    /// reacts" shape as OnPhaseTwo/OnBossDeath elsewhere in this file.
    /// </summary>
    IEnumerator StaggerRoutine()
    {
        IsStaggered = true;
        OnStaggerStart?.Invoke();

        yield return new WaitForSeconds(staggerDuration);

        IsStaggered = false;
        _staggerRoutine = null;

        if (_isDead) yield break; // died mid-stagger -- nothing left to resume

        OnStaggerEnd?.Invoke();
    }
}
