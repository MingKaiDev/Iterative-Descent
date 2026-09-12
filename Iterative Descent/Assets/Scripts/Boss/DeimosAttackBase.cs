using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Abstract base for all Deimos attacks. Mirrors BossAttackBase.cs's (ARES/DROID-7) contract
/// exactly -- deliberately a separate class rather than a shared one, matching this project's
/// established pattern of one dedicated script per boss (DeimosHealth vs BossHealth,
/// DeimosStateMachine vs BossStateMachine) even though this particular base class holds no
/// static state that would actually collide if shared. Keeping it separate means Deimos's own
/// attack roster (MMAKick now, Dropkick/JumpBack/etc. later) never has to worry about drifting
/// out of sync with whatever ARES-specific assumptions creep into BossAttackBase over time.
///
/// Subclass pattern (same as ARES's attacks):
///   1. Override PerformAttack() -- trigger the Animator, start any coroutines.
///   2. Add Animation Events on the clip:
///        - "OnHitboxOpen"    -- frame where the hit connects (override in subclass)
///        - "OnHitboxClose"   -- frame where the hit window ends (override in subclass, optional)
///        - "OnAttackAnimEnd" -- last frame of clip (signals attack complete)
///   3. Set attackTimeoutDuration in the Inspector to something safely longer than the clip.
///      The timeout is a safety net only -- the animation event is the primary signal.
///
/// All attack subclasses must sit on the same GameObject as the Animator so Unity's Animation
/// Event system can find them by method name.
/// </summary>
public abstract class DeimosAttackBase : MonoBehaviour
{
    // ─── Events ─────────────────────────────────────────────────────────────────

    /// <summary>Fired when the attack fully completes. DeimosAttackRegistry listens to exit Attacking state.</summary>
    public event Action OnAttackEnded;

    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Range")]
    [Tooltip("Maximum distance to player at which this attack can trigger. " +
             "Set to match the attack's actual reach so it never fires out of range.")]
    public float maxRange = 3f;

    [Tooltip("Minimum distance to player at which this attack can trigger. Defaults to 0 -- " +
             "every attack before Jump Forward (Move 6) leaves this untouched. Added specifically " +
             "for gap-closer moves that should only be picked once the player is TOO FAR for " +
             "Deimos's other attacks to reach (see JumpForwardAttack's minRange default of 6) -- " +
             "DeimosAttackRegistry now gates candidate selection on both minRange and maxRange " +
             "instead of maxRange alone, so this has no effect on any attack that leaves it at 0.")]
    public float minRange = 0f;

    [Header("Timing")]
    [Tooltip("Seconds before this attack can be used again after it ends.")]
    public float cooldown = 3f;

    [Tooltip("Safety net timeout. Set to clip length + 1s. If OnAttackAnimEnd animation event " +
             "is missing from the clip, this will end the attack instead.")]
    public float attackTimeoutDuration = 5f;

    [Header("Variety (RL-only)")]
    [Tooltip("Additional cooldown intended for future action-masking during training/inference -- " +
             "ignored by the current random-heuristic fallback (DeimosAttackRegistry.TryExecuteRandomAttack). " +
             "Kept for parity with BossAttackBase in case Deimos gets a training env later, same as ARES's.")]
    public float varietyCooldown = 3f;

    [Header("Audio")]
    [Tooltip("Plays automatically the instant this attack starts (Execute()). " +
             "Optional -- leave null for a silent windup.")]
    public AudioClip windupClip;

    [Tooltip("Plays at the moment this attack becomes dangerous (hitbox opens), whether or not " +
             "it actually connects with the player -- a whoosh/swing sound. Subclasses that " +
             "override OnHitboxOpen() call PlaySound(hitboxOpenClip) themselves, unconditionally, " +
             "at the top of that method -- see MMAKickAttack/DropkickAttack.")]
    public AudioClip hitboxOpenClip;

    [Tooltip("Plays only when this attack actually connects with the player (an IDamageable is " +
             "found in range) -- an impact/thud sound, distinct from hitboxOpenClip's whoosh " +
             "which plays regardless of whether anything was hit. Added 2026-09-11 alongside " +
             "MMAKick/Dropkick's knockback, as shared infrastructure so every future melee " +
             "attack subclass gets 'play a sound when it lands' for free -- each subclass calls " +
             "PlaySound(hitLandedClip) itself inside its own 'if (damageable != null)' branch " +
             "(see MMAKickAttack/DropkickAttack's OnHitboxOpen()), same place the knockback call " +
             "sits, so a hit that deals damage also plays a sound and pushes the player back in " +
             "one place.")]
    public AudioClip hitLandedClip;

    [Range(0f, 1f)]
    [Tooltip("Shared volume for all three attack SFX fields above.")]
    public float sfxVolume = 1f;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public bool IsOnCooldown => _cooldownTimer > 0f;
    public bool IsActive     => _isActive;

    /// <summary>RL-only. True while this attack is still within its varietyCooldown window
    /// after last being used. Unread by the existing random-heuristic path.</summary>
    public bool IsOnVarietyCooldown => _varietyTimer > 0f;

    /// <summary>RL-only. 0 immediately after use, ramping linearly to 1 once varietyCooldown
    /// has fully elapsed, then holding at 1.</summary>
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
    /// Fetches the shared AudioSource on Deimos's root GameObject. Subclasses do not currently
    /// override Awake, so this is safe to add without a base.Awake() call anywhere else in the
    /// hierarchy.
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
    /// Called by DeimosAttackRegistry to start the attack.
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
    /// coroutines in one call (the timeout watchdog plus any subclass movement/VFX coroutine),
    /// then clears cooldown/variety timers. Mirrors BossAttackBase.CancelAttack() -- kept for
    /// parity even though nothing calls it yet (Deimos has no checkpoint-respawn hookup or
    /// training env in this pass). Deliberately does NOT invoke OnAttackEnded, same reasoning
    /// as ARES's version: a future caller resetting Deimos wholesale is already about to force
    /// DeimosStateMachine back to a known state itself.
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

    // ─── Knockback Helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Pushes a hit player directly away from Deimos. Mirrors BruteAttack.ApplyKnockback's
    /// pattern -- the only other place in this codebase that calls
    /// PlayerMovement.ApplyKnockback -- hoisted here instead of duplicated per attack
    /// subclass, since more than one Deimos attack (MMAKick, Dropkick, and likely a future
    /// Flying Knee) wants the same "shove away from point of impact" behaviour. Note this is
    /// unlike GroundSlamAttack.cs (ARES) despite the "similar to ground slam" request that
    /// prompted this -- that attack turned out to only deal AOE damage plus a purely visual
    /// shockwave ring (GroundSlamVFX), with no actual PlayerMovement.ApplyKnockback call
    /// anywhere in it. BruteAttack's swipe-hit knockback is the real (and only) precedent
    /// for an actual physics push in this codebase, so that's the pattern mirrored here.
    ///
    /// Safe to call with any hit Collider -- no-ops if it has no PlayerMovement component
    /// (e.g. it matched playerLayer but isn't actually the player), and
    /// PlayerMovement.ApplyKnockback itself no-ops once the player is dead as a second line
    /// of defence.
    /// </summary>
    /// <param name="hit">The player's Collider, as returned by the attack's own OverlapSphere check.</param>
    /// <param name="force">Horizontal speed (m/s) given to the player, away from Deimos.</param>
    /// <param name="upwardKick">Small upward component added on top, purely for stagger feel.</param>
    protected void ApplyKnockbackTo(Collider hit, float force, float upwardKick)
    {
        var playerMovement = hit.GetComponent<PlayerMovement>();
        if (playerMovement == null) return;

        Vector3 dir = hit.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward; // degenerate: same position

        dir.Normalize();
        playerMovement.ApplyKnockback(dir * force + Vector3.up * upwardKick);
    }
}
