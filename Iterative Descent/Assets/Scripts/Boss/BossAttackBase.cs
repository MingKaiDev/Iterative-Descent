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

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public bool IsOnCooldown => _cooldownTimer > 0f;
    public bool IsActive     => _isActive;

    // ─── Private ────────────────────────────────────────────────────────────────
    private float   _cooldownTimer;
    private bool    _isActive;
    private Coroutine _timeoutRoutine;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    protected virtual void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;
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
        _timeoutRoutine = StartCoroutine(AttackTimeoutRoutine());
        PerformAttack();
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
    public void OnAttackAnimEnd()
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

        OnAttackEnded?.Invoke();
    }

    IEnumerator AttackTimeoutRoutine()
    {
        yield return new WaitForSeconds(attackTimeoutDuration);
        Debug.LogWarning($"[{GetType().Name}] Attack timed out after {attackTimeoutDuration}s. " +
                         "Add an OnAttackAnimEnd Animation Event to the clip to avoid this.");
        EndAttack();
    }
}
