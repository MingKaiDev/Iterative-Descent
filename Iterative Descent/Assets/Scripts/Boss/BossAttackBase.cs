using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Abstract base for all DROID-7 attacks.
///
/// Subclass pattern:
///   1. Override PerformAttack() -- trigger the Animator, start any coroutines.
///   2. Add Animation Events on the clip:
///        - "OnHitboxOpen"  -- frame where the hit connects (override in subclass)
///        - "OnHitboxClose" -- frame where the hit window ends (override in subclass, optional)
///        - "OnAttackAnimEnd" -- last frame of clip (signals attack complete)
///   3. Set attackTimeoutDuration in the Inspector to something safely longer than the clip.
///      The timeout is a safety net only -- the animation event is the primary signal.
///
/// All attack subclasses must sit on the same GameObject as the Animator
/// so Unity's Animation Event system can find them by method name.
/// </summary>
public abstract class BossAttackBase : MonoBehaviour
{
    // ─── Events ─────────────────────────────────────────────────────────────────

    /// <summary>Fired when the attack fully completes. BossAttackRegistry listens to exit Attacking state.</summary>
    public event Action OnAttackEnded;

    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Range")]
    [Tooltip("Maximum distance to player at which this attack can trigger. " +
             "Set to match the attack's actual reach so it never fires out of range.")]
    public float maxRange = 3f;

    [Header("Timing")]
    [Tooltip("Seconds before this attack can be used again after it ends.")]
    public float cooldown = 3f;

    [Tooltip("Safety net timeout. Set to clip length + 1s. If OnAttackAnimEnd animation event " +
             "is missing from the clip, this will end the attack instead.")]
    public float attackTimeoutDuration = 5f;

    [Header("Variety (RL-only)")]
    [Tooltip("Additional cooldown used ONLY by BossAgent's action masking during training/inference -- " +
             "ignored by the existing random-heuristic fallback (BossAttackRegistry.TryExecuteRandomAttack), " +
             "so this has zero effect on current gameplay until BossAgent exists and reads it. Independent " +
             "from 'cooldown' above -- an attack is only variety-eligible once BOTH have cleared.")]
    public float varietyCooldown = 3f;

    [Header("Audio")]
    [Tooltip("Plays automatically the instant this attack starts (Execute()). " +
             "Optional -- leave null for a silent windup.")]
    public AudioClip windupClip;

    [Tooltip("Plays at the moment this attack becomes dangerous. Subclasses that override " +
             "OnHitboxOpen() call PlaySound(hitboxOpenClip) themselves -- see SlashAttack for " +
             "the pattern. SprintChargeAttack plays it from its charge coroutine instead, since " +
             "its OnHitboxOpen() is deliberately suppressed for damage reasons.")]
    public AudioClip hitboxOpenClip;

    [Range(0f, 1f)]
    [Tooltip("Shared volume for both attack SFX fields above.")]
    public float sfxVolume = 1f;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public bool IsOnCooldown => _cooldownTimer > 0f;
    public bool IsActive     => _isActive;

    /// <summary>RL-only. True while this attack is still within its varietyCooldown window
    /// after last being used. Unread by the existing random-heuristic path.</summary>
    public bool IsOnVarietyCooldown => _varietyTimer > 0f;

    /// <summary>RL-only. 0 immediately after use, ramping linearly to 1 once varietyCooldown
    /// has fully elapsed, then holding at 1. Feeds BossAgent's "time since last used" observation
    /// -- same underlying timer as IsOnVarietyCooldown, just exposed as a continuous 0-1 value
    /// instead of a bool.</summary>
    public float TimeSinceUsedNormalized => varietyCooldown > 0f
        ? Mathf.Clamp01((varietyCooldown - _varietyTimer) / varietyCooldown)
        : 1f;

    // ─── Private ────────────────────────────────────────────────────────────────
    private float   _cooldownTimer;
    private float   _varietyTimer;
    private bool    _isActive;
    private Coroutine _timeoutRoutine;
    private AudioSource _sfxSource;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the shared AudioSource on the boss root GameObject (the same one
    /// BossAudio uses). Subclasses do not currently override Awake, so this is
    /// safe to add without a base.Awake() call anywhere else in the hierarchy.
    /// </summary>
    protected virtual void Awake()
    {
        _sfxSource = GetComponent<AudioSource>();
    }

    protected virtual void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (_varietyTimer > 0f)
            _varietyTimer -= Time.deltaTime;
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by BossAttackRegistry to start the attack.
    /// Guards against double-execution and cooldown.
    /// </summary>
    public void Execute()
    {
        if (_isActive || IsOnCooldown) return;

        _isActive = true;
        PlaySound(windupClip);
        _timeoutRoutine = StartCoroutine(AttackTimeoutRoutine());
        PerformAttack();
    }

    // ─── Audio Helper ───────────────────────────────────────────────────────────

    /// <summary>
    /// Plays a one-shot clip on the shared boss AudioSource. Safe to call with a
    /// null clip (does nothing) so subclasses don't need their own null checks.
    /// </summary>
    protected void PlaySound(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    /// <summary>
    /// Called by Animation Event at the frame where the hit should land.
    /// Override in subclasses to apply damage / spawn projectiles.
    /// </summary>
    public virtual void OnHitboxOpen() { }

    /// <summary>
    /// Called by Animation Event when the hit window closes (optional).
    /// </summary>
    public virtual void OnHitboxClose() { }

    /// <summary>
    /// Called by Animation Event on the final frame of the attack clip.
    /// This is the primary signal that the attack is done.
    /// </summary>
    public virtual void OnAttackAnimEnd()
    {
        EndAttack();
    }

    // ─── Abstract ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Trigger the Animator and start any movement coroutines for this attack.
    /// Do NOT call EndAttack() here -- rely on the animation event.
    /// </summary>
    protected abstract void PerformAttack();

    // ─── Internal ────────────────────────────────────────────────────────────────

    protected void EndAttack()
    {
        if (!_isActive) return;

        if (_timeoutRoutine != null)
        {
            StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = null;
        }

        _isActive      = false;
        _cooldownTimer = cooldown;
        _varietyTimer  = varietyCooldown;

        OnAttackEnded?.Invoke();
    }

    /// <summary>
    /// Forcibly aborts this attack if it is currently active. Stops ALL of this component's
    /// coroutines in one call -- the timeout watchdog AND any subclass movement/VFX coroutine --
    /// then clears cooldown/variety timers so the attack is immediately available again next
    /// episode. Deliberately does NOT invoke OnAttackEnded: the caller (BossTrainingEnv.
    /// ResetEpisode(), Training.unity only) is already about to force BossStateMachine back to
    /// Combat itself via ResetForNewEpisode(), so firing that event here would be redundant --
    /// and, worse, would fire BEFORE the boss's position/health have actually been reset.
    ///
    /// Added 2026-08-27: attacks that seize manual control mid-coroutine (e.g. SprintChargeAttack/
    /// GroundSlamAttack disconnecting the NavMeshAgent, or PounceAttack hiding renderers) had no
    /// way to know the boss died mid-attack. Their coroutines kept running to completion using
    /// stale pre-death data, silently overwriting BossTrainingEnv's fresh episode reset -- for the
    /// NavMeshAgent case this could drag the boss off the baked NavMesh, which then made
    /// NavMeshAgent.Warp()/SetDestination() fail silently for the rest of the session. Subclasses
    /// that take manual control of the agent, renderers, or other lingering visual/physical state
    /// MUST override this and restore it -- see SprintChargeAttack/GroundSlamAttack/PounceAttack/
    /// CoreOverloadAttack for the pattern. Base implementation is safe as-is for attacks with no
    /// such state (SlashAttack, LeftPunchAttack, StabAttack, BladeSweepAttack -- none of these
    /// touch the NavMeshAgent or hold any state beyond the base class's own).
    /// </summary>
    public virtual void CancelAttack()
    {
        if (!_isActive) return;

        StopAllCoroutines();
        _timeoutRoutine = null;
        _isActive       = false;
        _cooldownTimer  = 0f;
        _varietyTimer   = 0f;
    }

    IEnumerator AttackTimeoutRoutine()
    {
        yield return new WaitForSeconds(attackTimeoutDuration);
        Debug.LogWarning($"[{GetType().Name}] Attack timed out after {attackTimeoutDuration}s. " +
                         "Add an OnAttackAnimEnd Animation Event to the clip to avoid this.");
        EndAttack();
    }
}
