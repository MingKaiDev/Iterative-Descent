using UnityEngine;
using System.Collections;

/// <summary>
/// Handles periodic melee attacks for the enemy.
/// Called by EnemyChaser.TryAttack() each frame while in attack range.
///
/// ─── How _isAttacking resets ──────────────────────────────────────────────
/// A coroutine resets _isAttacking after attackCooldown seconds.
/// This means animation events are OPTIONAL — the system works without them.
/// When you add the attack animation, also add these two Animation Events
/// on the clip for the hitbox window:
///   Impact frame → OnAttackHitFrame()
///   Last frame   → OnAttackEnd()        (optional — coroutine handles it too)
///
/// ─── Fist hitbox setup ────────────────────────────────────────────────────
///   Root (EnemyChaser + EnemyAttack + NavMeshAgent)
///   └── CharacterRig (Animator)
///       └── ... → Hand_R bone
///           └── FistHitbox  (BoxCollider IsTrigger=true + FistHitboxRelay)
///                                           ↑ assign to fistHitbox in Inspector
///
/// ─── Animator parameter ───────────────────────────────────────────────────
///   Trigger  "Attack"  — must exist in the Animator Controller
/// </summary>
public class EnemyAttack : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Attack Settings")]
    [Tooltip("Seconds between attack swings.")]
    [SerializeField] private float attackCooldown = 1.8f;
    [Tooltip("Raw damage dealt per hit.")]
    [SerializeField] private float meleeDamage = 20f;
    [Tooltip("Duration in seconds the fist hitbox stays active per swing.")]
    [SerializeField] private float hitboxActiveWindow = 0.12f;
    [Tooltip("Seconds after trigger fires before the hitbox opens. " +
             "Match this to the windup time in your animation.")]
    [SerializeField] private float hitboxDelay = 0.4f;

    [Header("Fist Hitbox — leave null until rig is imported")]
    [Tooltip("The trigger Collider on the fist bone.")]
    [SerializeField] private Collider fistHitbox;

    [Header("Animator — leave empty, auto-found in children")]
    [SerializeField] private Animator animator;

    // ─── Cached hashes ────────────────────────────────────────────────────────

    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");

    // ─── Private state ────────────────────────────────────────────────────────

    private float   _baseMeleeDamage;  // original inspector value — never changes after Awake
    private float   _cooldownTimer;
    private bool    _isAttacking;   // true while an attack swing is in progress
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
        _cooldownTimer = attackCooldown;

        // Fire the animator trigger if available
        if (animator != null)
            animator.SetTrigger(AttackTriggerHash);

        // Coroutine handles the hitbox window AND resets _isAttacking.
        // This works whether or not animation events are wired up.
        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(AttackSequence());

        Debug.Log("[EnemyAttack] Attack started.");
    }

    /// <summary>
    /// Drives the hitbox open/close window and resets _isAttacking.
    /// Runs independently of animation events so the system works without them.
    /// </summary>
    private IEnumerator AttackSequence()
    {
        // Wait for the windup (animation reaches impact frame)
        yield return new WaitForSeconds(hitboxDelay);

        // Open hitbox
        if (fistHitbox != null)
        {
            fistHitbox.enabled = true;
            Debug.Log("[EnemyAttack] Fist hitbox OPEN.");
        }
        else
        {
            Debug.Log("[EnemyAttack] STUB — no fistHitbox assigned yet. " +
                      "Assign once rig is imported.");
        }

        // Keep hitbox open for the active window
        yield return new WaitForSeconds(hitboxActiveWindow);

        // Close hitbox
        DisableFistHitbox();

        // Release the attack lock — next swing can now queue
        _isAttacking = false;
        _attackRoutine = null;
    }

    // ─── Animation Event receivers (OPTIONAL — system works without them) ─────

    /// <summary>
    /// [Optional Animation Event] — place on the impact frame of the Attack clip.
    /// If present, opens the hitbox at the exact animation frame instead of
    /// relying on hitboxDelay timing. The coroutine is still running; calling
    /// this early just opens the hitbox sooner.
    /// </summary>
    public void OnAttackHitFrame()
    {
        if (fistHitbox == null) return;
        // Stop the coroutine's timed open so we don't double-enable
        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        fistHitbox.enabled = true;
        CancelInvoke(nameof(DisableFistHitbox));
        Invoke(nameof(DisableFistHitbox), hitboxActiveWindow);
    }

    /// <summary>
    /// [Optional Animation Event] — place on the last frame of the Attack clip.
    /// Resets state early if animation events are wired. Safe to skip.
    /// </summary>
    public void OnAttackEnd()
    {
        _isAttacking = false;
        DisableFistHitbox();
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    // ─── Called by FistHitboxRelay on the fist bone ───────────────────────────

    /// <summary>
    /// Relayed from FistHitboxRelay.cs. Deals damage to the first IDamageable hit.
    /// </summary>
    public void OnFistHit(Collider other)
    {
        if (fistHitbox == null || !fistHitbox.enabled) return;
        if (other.transform.IsChildOf(transform)) return;     // ignore self

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null) return;

        Vector3 hitPoint = other.ClosestPoint(fistHitbox.transform.position);
        target.TakeDamage(meleeDamage, hitPoint);
        Debug.Log($"[EnemyAttack] Hit '{other.name}' for {meleeDamage} dmg.");

        // One hit per swing — close hitbox immediately after landing
        DisableFistHitbox();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void DisableFistHitbox()
    {
        if (fistHitbox != null)
            fistHitbox.enabled = false;
    }
}
