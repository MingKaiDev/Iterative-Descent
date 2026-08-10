using UnityEngine;
using System.Collections;

/// <summary>
/// Handles periodic melee attacks for the enemy.
/// Called by EnemyChaser.TryAttack() each frame while in attack range.
///
/// ─── Hit detection: one-shot OverlapBox, not a toggled trigger ───────────
/// Previously this toggled a trigger Collider on/off for a short window and
/// relied on OnTriggerEnter via FistHitboxRelay. That has two problems: (1)
/// Unity only evaluates trigger overlaps on the physics step, so a short
/// enable/disable window can fall between two ticks and miss entirely, and
/// (2) it was purely time-guessed with no way to verify it matched the real
/// animation. Fixed by switching to a single Physics.OverlapBox check fired
/// at exactly hitboxDelay (or from an Animation Event, see below) -- same
/// pattern already used by RusherAttack.cs. FistHitboxRelay is no longer
/// needed on this bone; remove it if present.
///
/// ─── How _isAttacking resets ──────────────────────────────────────────────
/// A coroutine resets _isAttacking after attackCooldown seconds and runs the
/// hit check at hitboxDelay as a fallback if no Animation Event fires first.
/// This means animation events are OPTIONAL — the system works without them
/// — but the fallback delay is only as accurate as the value you tune it to.
/// For frame-accurate hits, add an Animation Event on the clip's impact
/// frame calling OnAttackHitFrame(). Watch the "[EnemyAttack] HIT CHECK
/// ACTIVE" log against the swing in Play mode to tune hitboxDelay if you are
/// not using an event.
///   Impact frame → OnAttackHitFrame()   (optional — enables frame-accurate hits)
///   Last frame   → OnAttackEnd()        (optional — coroutine handles it too)
///
/// ─── Fist hitbox setup ────────────────────────────────────────────────────
///   Root (EnemyChaser + EnemyAttack + NavMeshAgent)
///   └── CharacterRig (Animator)
///       └── ... → Hand_R bone
///           └── FistHitbox  (BoxCollider, Is Trigger = true, NEVER enabled at
///                            runtime — position/size reference only)
///                                           ↑ assign to fistHitbox in Inspector
///
/// ─── Animator parameter ───────────────────────────────────────────────────
///   Trigger  "Attack"  — must exist in the Animator Controller
///
/// ─── Custom animation callback (EnemyRusher and future variants) ─────────
///   Set OnAttackFired to a delegate to supply your own animator trigger.
///   When set, the built-in "Attack" trigger is suppressed so only your
///   delegate fires. EnemyChaser leaves this null -- no behaviour change.
/// </summary>
public class EnemyAttack : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Attack Settings")]
    [Tooltip("Seconds between attack swings.")]
    [SerializeField] private float attackCooldown = 1.8f;
    [Tooltip("Raw damage dealt per hit.")]
    [SerializeField] private float meleeDamage = 20f;
    [Tooltip("Seconds after the attack trigger fires before the hit check runs " +
             "(fallback if no Animation Event calls OnAttackHitFrame). Match this " +
             "to the impact frame in your animation clip -- watch the " +
             "[EnemyAttack] HIT CHECK log against the swing in Play mode and " +
             "adjust until they line up.")]
    [SerializeField] private float hitboxDelay = 0.4f;

    [Header("Fist Hitbox — sizing reference only, never enabled at runtime")]
    [Tooltip("The BoxCollider on the fist bone. Used for position/size only. " +
             "NOTE: this field's type changed from Collider to BoxCollider -- " +
             "re-assign it in the Inspector if the reference shows empty.")]
    [SerializeField] private BoxCollider fistHitbox;

    [Header("Hit Detection")]
    [Tooltip("Layers the OverlapBox check hits. Set this to the Player layer.")]
    [SerializeField] private LayerMask hitableLayers = ~0;

    [Header("Animator — leave empty, auto-found in children")]
    [SerializeField] private Animator animator;

    // ─── Cached hashes ────────────────────────────────────────────────────────

    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");

    // ─── Public callback (optional) ───────────────────────────────────────────

    /// <summary>
    /// When assigned, called instead of the built-in "Attack" animator trigger.
    /// Use this to supply a custom animation (e.g. alternating swipes) from a
    /// subclass or companion script without modifying EnemyAttack internals.
    /// Leave null to use the default "Attack" trigger (EnemyChaser behaviour).
    /// </summary>
    public System.Action OnAttackFired;

    /// <summary>
    /// Fired at the start of every swing, before the animator trigger. Passes
    /// hitboxDelay -- the seconds until the swing resolves -- so a sibling
    /// component (e.g. EnemyChaser) can time a windup animation/rotation to
    /// finish exactly when the hit check runs, instead of using a separate,
    /// independently-tuned duration that can drift out of sync.
    /// </summary>
    public event System.Action<float> OnAttackWindupStart;

    /// <summary>
    /// Fired once the swing resolves -- exactly when RunHitCheck() runs,
    /// whether that came from an Animation Event or the hitboxDelay fallback.
    /// Pairs with OnAttackWindupStart to bracket a windup pose.
    /// </summary>
    public event System.Action OnAttackWindupEnd;

    // ─── Private state ────────────────────────────────────────────────────────

    private float   _baseMeleeDamage;  // original inspector value — never changes after Awake
    private float   _cooldownTimer;
    private bool    _isAttacking;   // true while an attack swing is in progress
    private bool    _hitCheckDone;  // guards against double-check (event fired + coroutine fallback)
    private Coroutine _attackRoutine;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (fistHitbox != null)
            fistHitbox.enabled = false;

        _baseMeleeDamage = meleeDamage;
    }

    // ─── DDA API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a DDA damage multiplier relative to the base inspector value.
    /// Safe to call multiple times — always multiplies from the original base.
    /// Called by EnemyChaser.OnActivate() via EnemyDirector.
    /// </summary>
    public void ApplyDamageMultiplier(float multiplier)
    {
        meleeDamage = _baseMeleeDamage * multiplier;
        Debug.Log($"[EnemyAttack] Damage set to {meleeDamage:F1} (base {_baseMeleeDamage:F1} x{multiplier:F2})");
    }

    // ─── Per-attack-variant timing override ────────────────────────────────────

    /// <summary>
    /// Overrides hitboxDelay for the NEXT swing only. Call this from an
    /// OnAttackFired subscriber, before that subscriber fires its own animator
    /// trigger, when different animation clips have different impact timing
    /// (e.g. EnemyChaser alternating Punch1/Punch2 -- two clips of very
    /// different lengths cannot share one correct delay). If never called,
    /// the Inspector-set hitboxDelay value is used, unchanged.
    /// </summary>
    public void SetHitboxDelay(float delay)
    {
        hitboxDelay = delay;
    }

    // ─── Public API ── called by EnemyChaser every frame in attack state ──────

    /// <summary>
    /// Call continuously while in attack range. Handles cooldown internally.
    /// </summary>
    public void TryAttack()
    {
        if (_isAttacking) return;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer > 0f) return;

        StartAttack();
    }

    // ─── Core attack flow ─────────────────────────────────────────────────────

    private void StartAttack()
    {
        _isAttacking   = true;
        _hitCheckDone  = false;
        _cooldownTimer = attackCooldown;

        // Fire the animator trigger BEFORE announcing windup start. If a custom
        // callback is assigned (e.g. EnemyChaser choosing Punch1 vs Punch2, or
        // EnemyRusher supplying alternating swipe triggers), it may call
        // SetHitboxDelay() to override timing for THIS swing -- doing this
        // first means OnAttackWindupStart below picks up the corrected value
        // instead of whatever hitboxDelay was left at from the previous swing.
        if (OnAttackFired != null)
            OnAttackFired.Invoke();
        else if (animator != null)
            animator.SetTrigger(AttackTriggerHash);

        OnAttackWindupStart?.Invoke(hitboxDelay);

        // Coroutine runs the fallback hit check AND resets _isAttacking.
        // This works whether or not an Animation Event is wired up.
        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(AttackSequence());

        Debug.Log("[EnemyAttack] Attack started.");
    }

    /// <summary>
    /// Fallback timing: runs the hit check at hitboxDelay if no Animation Event
    /// called OnAttackHitFrame first, then releases the attack lock.
    /// </summary>
    private IEnumerator AttackSequence()
    {
        yield return new WaitForSeconds(hitboxDelay);

        if (!_hitCheckDone)
            RunHitCheck();

        _isAttacking   = false;
        _attackRoutine = null;
    }

    // ─── Animation Event receivers (OPTIONAL — system works without them) ─────

    /// <summary>
    /// [Animation Event] — place on the impact frame of the Attack clip for a
    /// frame-accurate hit check instead of relying on the guessed hitboxDelay.
    /// Safe to skip; the coroutine fallback in AttackSequence() covers it.
    /// </summary>
    public void OnAttackHitFrame()
    {
        if (_hitCheckDone) return;
        RunHitCheck();
    }

    /// <summary>
    /// [Optional Animation Event] — place on the last frame of the Attack clip.
    /// Resets state early if animation events are wired. Safe to skip.
    /// </summary>
    public void OnAttackEnd()
    {
        _isAttacking = false;
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    // ─── Hit check ──────────────────────────────────────────────────────────────

    /// <summary>
    /// One-shot OverlapBox at the fist's current world position/size. Damage
    /// only registers at the exact moment this runs, so there is no
    /// "already overlapping when the collider turned on" or "missed physics
    /// tick" problem that the old toggled-trigger approach had.
    /// </summary>
    private void RunHitCheck()
    {
        _hitCheckDone = true;
        OnAttackWindupEnd?.Invoke();

        Debug.Log($"[EnemyAttack] HIT CHECK ACTIVE | delay={hitboxDelay:F2}s | time={Time.time:F2}");

        if (fistHitbox == null)
        {
            Debug.Log("[EnemyAttack] STUB — no fistHitbox assigned yet. " +
                      "Assign once rig is imported.");
            return;
        }

        Vector3 worldCenter = fistHitbox.transform.TransformPoint(fistHitbox.center);
        Vector3 halfExtents = Vector3.Scale(fistHitbox.size * 0.5f, AbsScale(fistHitbox.transform.lossyScale));

        // Collide (not Ignore) -- the player hurtbox is expected to be a trigger
        // collider (enlarged, non-blocking) rather than the CharacterController's
        // own solid capsule. hitableLayers already restricts this to the Player
        // layer, so this doesn't start picking up unrelated scene triggers.
        Collider[] hits = Physics.OverlapBox(
            worldCenter, halfExtents, fistHitbox.transform.rotation,
            hitableLayers, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;     // ignore self

            // Require specifically the player, not "anything IDamageable" --
            // EnemyBase also implements IDamageable (so player bullets can hit
            // enemies via the same interface), so a generic IDamageable lookup
            // here can resolve to a neighbouring enemy instead of the player
            // when enemies are clustered. See project-enemy-system.md 2026-08-10.
            PlayerHealth target = hit.GetComponentInParent<PlayerHealth>();
            if (target == null) continue;

            Vector3 hitPoint = hit.ClosestPoint(worldCenter);
            target.TakeDamage(meleeDamage, hitPoint);
            Debug.Log($"[EnemyAttack] Hit '{hit.name}' for {meleeDamage} dmg.");
            return; // one hit per swing
        }

        Debug.Log("[EnemyAttack] Swing missed.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Component-wise absolute value — avoids negative half-extents from negative scale.</summary>
    private static Vector3 AbsScale(Vector3 v) =>
        new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // ─── Scene gizmos ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (fistHitbox == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            fistHitbox.transform.TransformPoint(fistHitbox.center),
            fistHitbox.transform.rotation,
            fistHitbox.transform.lossyScale);
        Gizmos.DrawWireCube(Vector3.zero, fistHitbox.size);
        Gizmos.matrix = prev;
    }
#endif
}
