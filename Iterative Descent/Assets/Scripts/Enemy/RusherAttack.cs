using UnityEngine;
using System.Collections;

/// <summary>
/// Melee attack component for EnemyRusher.
/// Manages cooldown, alternates Right Swipe / Left Swipe animator triggers,
/// and detects hits via Physics.OverlapBox at the correct impact frame.
///
/// This replaces EnemyAttack on the Rusher prefab. EnemyChaser still uses
/// EnemyAttack -- these two scripts are intentionally separate.
///
/// ─── Why OverlapBox instead of trigger colliders ──────────────────────────
/// Enabling a trigger collider that already overlaps the player causes
/// OnTriggerEnter to fire immediately (before the animation arm moves).
/// OverlapBox is a one-shot check that runs at exactly the hitboxDelay
/// offset, so damage only registers at the right animation frame.
///
/// ─── hitboxDelay is still a guess unless you add an Animation Event ──────
/// The one-shot check removes the "missed physics tick" failure mode, but
/// hitboxDelay itself is only as accurate as the value it's tuned to -- if
/// it doesn't match the real windup in the clip, hits will still feel off.
/// For frame-accurate hits, add an Animation Event on each swipe clip's
/// impact frame. Animator events can't pass a bool parameter, so there are
/// two receivers instead of one: OnRightHitFrame() on the Right Swipe clip,
/// OnLeftHitFrame() on the Left Swipe clip. Watch the "[RusherAttack] HIT
/// CHECK ACTIVE" log against the swing in Play mode to tune hitboxDelay if
/// you are not using events.
///
/// ─── Hitbox setup ─────────────────────────────────────────────────────────
///   Root (EnemyRusher + RusherAttack + NavMeshAgent)
///   └── CharacterRig (Animator)
///       └── ... Hand_R bone
///           └── RightFistHitbox  child object with BoxCollider
///                                Is Trigger = true  (visual sizing only, never enabled)
///                                assign to rightFistBox in Inspector
///       └── ... Hand_L bone
///           └── LeftFistHitbox   same setup
///                                assign to leftFistBox in Inspector
///
///   RusherHitboxRelay is no longer needed and can be removed from the bones.
///
/// ─── Animator parameters (configured here, NOT in EnemyRusher) ───────────
///   Trigger  "Right Swipe"  -- fired on odd swings  (1st, 3rd, ...)
///   Trigger  "Left Swipe"   -- fired on even swings (2nd, 4th, ...)
///
/// ─── Animation Events (optional) ─────────────────────────────────────────
///   Impact frame of Right Swipe clip → OnRightHitFrame()  (frame-accurate hit)
///   Impact frame of Left Swipe clip  → OnLeftHitFrame()   (frame-accurate hit)
///   Last frame of either clip        → OnAttackEnd()      (resets state early)
///   All three are optional -- the coroutine fallback (hitboxDelay) handles
///   hit detection and state reset regardless of whether events are wired.
/// </summary>
public class RusherAttack : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Attack Settings")]
    [Tooltip("Seconds between swings.")]
    [SerializeField] private float attackCooldown = 1.5f;
    [Tooltip("Raw damage per hit.")]
    [SerializeField] private float meleeDamage    = 25f;
    [Tooltip("Seconds after the animator trigger fires before the hit check runs. " +
             "Increase this if damage fires before the visual impact. " +
             "Match it to the windup duration in your animation clip.")]
    [SerializeField] private float hitboxDelay    = 0.45f;

    [Header("Hitboxes -- assign the BoxCollider on each hand bone child")]
    [Tooltip("BoxCollider on the right hand bone child. Used for position and size only -- never enabled at runtime.")]
    [SerializeField] private BoxCollider rightFistBox;
    [Tooltip("BoxCollider on the left hand bone child. Used for position and size only -- never enabled at runtime.")]
    [SerializeField] private BoxCollider leftFistBox;

    [Header("Hit Detection")]
    [Tooltip("Layers the OverlapBox check hits. Set this to the Player layer.")]
    [SerializeField] private LayerMask hitableLayers = ~0;

    [Header("Animator -- leave empty, auto-found in children")]
    [SerializeField] private Animator animator;

    [Header("Animator Parameter Names")]
    [SerializeField] private string rightSwipeParam = "Right Swipe";
    [SerializeField] private string leftSwipeParam  = "Left Swipe";

    // ─── Cached hashes ────────────────────────────────────────────────────────

    private int _rightSwipeHash;
    private int _leftSwipeHash;

    // ─── Private state ────────────────────────────────────────────────────────

    private float     _baseMeleeDamage;
    private float     _cooldownTimer;
    private bool      _isAttacking;
    private bool      _nextSwipeRight = true;
    private bool      _hitCheckDone;     // guards against double-check (event fired + coroutine fallback)
    private Coroutine _attackRoutine;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // BoxColliders are never enabled at runtime -- they are sizing references only.
        // Disable them now so they cannot generate physics events or push the player.
        if (rightFistBox != null) rightFistBox.enabled = false;
        if (leftFistBox  != null) leftFistBox.enabled  = false;

        _baseMeleeDamage = meleeDamage;
        _rightSwipeHash  = Animator.StringToHash(rightSwipeParam);
        _leftSwipeHash   = Animator.StringToHash(leftSwipeParam);
    }

    // ─── DDA API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a DDA multiplier relative to the base inspector value.
    /// Safe to call repeatedly -- always multiplies from the original base.
    /// </summary>
    public void ApplyDamageMultiplier(float multiplier)
    {
        meleeDamage = _baseMeleeDamage * multiplier;
        Debug.Log($"[RusherAttack] Damage set to {meleeDamage:F1} " +
                  $"(base {_baseMeleeDamage:F1} x{multiplier:F2})");
    }

    /// <summary>
    /// Resets the swipe sequence so the next attack is always a right swipe.
    /// Called from EnemyRusher.OnActivate() to keep encounters consistent.
    /// </summary>
    public void ResetSwipeSequence()
    {
        _nextSwipeRight = true;
    }

    // ─── Public API ── called by EnemyRusher every frame in Mauling state ─────

    /// <summary>
    /// Attempt to start an attack swing. Handles cooldown internally.
    /// Call continuously while in Mauling state.
    /// </summary>
    public void TryAttack()
    {
        if (_isAttacking) return;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer > 0f) return;

        StartSwipe();
    }

    // ─── Core attack flow ─────────────────────────────────────────────────────

    private void StartSwipe()
    {
        _isAttacking   = true;
        _hitCheckDone  = false;
        _cooldownTimer = attackCooldown;

        bool isRight    = _nextSwipeRight;
        _nextSwipeRight = !_nextSwipeRight;

        if (animator != null)
            animator.SetTrigger(isRight ? _rightSwipeHash : _leftSwipeHash);

        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(SwipeSequence(isRight));

        Debug.Log($"[RusherAttack] {(isRight ? "Right" : "Left")} swipe started.");
    }

    /// <summary>
    /// Fallback timing: waits hitboxDelay then runs the hit check if no
    /// Animation Event (OnRightHitFrame/OnLeftHitFrame) called it first.
    /// </summary>
    private IEnumerator SwipeSequence(bool isRight)
    {
        yield return new WaitForSeconds(hitboxDelay);

        if (!_hitCheckDone)
            RunHitCheck(isRight);

        _isAttacking   = false;
        _attackRoutine = null;
    }

    // ─── Animation Event receivers ─────────────────────────────────────────────

    /// <summary>
    /// [Animation Event] -- place on the impact frame of the Right Swipe clip
    /// for a frame-accurate hit check instead of relying on the guessed
    /// hitboxDelay. Safe to skip; the coroutine fallback covers it.
    /// </summary>
    public void OnRightHitFrame()
    {
        if (_hitCheckDone) return;
        RunHitCheck(true);
    }

    /// <summary>
    /// [Animation Event] -- place on the impact frame of the Left Swipe clip.
    /// See OnRightHitFrame().
    /// </summary>
    public void OnLeftHitFrame()
    {
        if (_hitCheckDone) return;
        RunHitCheck(false);
    }

    /// <summary>
    /// [Optional Animation Event] -- place on the last frame of each swipe clip.
    /// Resets the attack lock early if wired. The coroutine handles it regardless.
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
    /// One-shot OverlapBox check at the given hand's current world position.
    /// Damage only registers at the exact moment this runs, so there is no
    /// "already overlapping" problem.
    /// </summary>
    private void RunHitCheck(bool isRight)
    {
        _hitCheckDone = true;

        BoxCollider box  = isRight ? rightFistBox : leftFistBox;
        string      side = isRight ? "right" : "left";

        // Tuning log -- shows which hand fired and at what timestamp.
        // Adjust hitboxDelay until this appears in sync with the visual impact frame
        // (only relevant if you are not using OnRightHitFrame/OnLeftHitFrame events).
        Debug.Log($"[RusherAttack] HIT CHECK ACTIVE -- {side} hand | " +
                  $"delay={hitboxDelay:F2}s | time={Time.time:F2}");

        if (box == null)
        {
            Debug.Log($"[RusherAttack] STUB -- {side}FistBox not assigned yet.");
            return;
        }

        // Compute world-space centre and half-extents from the BoxCollider.
        // lossyScale accounts for any scaling on the bone or its parents.
        Vector3 worldCenter  = box.transform.TransformPoint(box.center);
        Vector3 halfExtents  = Vector3.Scale(box.size * 0.5f, AbsScale(box.transform.lossyScale));

        // Collide (not Ignore) -- the player hurtbox is expected to be a trigger
        // collider (enlarged, non-blocking) rather than the CharacterController's
        // own solid capsule. hitableLayers already restricts this to the Player
        // layer, so this doesn't start picking up unrelated scene triggers.
        Collider[] hits = Physics.OverlapBox(
            worldCenter, halfExtents, box.transform.rotation,
            hitableLayers, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;     // skip own colliders

            // Require specifically the player, not "anything IDamageable" --
            // EnemyBase also implements IDamageable (so player bullets can hit
            // enemies via the same interface), so a generic IDamageable lookup
            // here can resolve to a neighbouring enemy instead of the player
            // when enemies are clustered. See project-enemy-system.md 2026-08-10.
            PlayerHealth target = hit.GetComponentInParent<PlayerHealth>();
            if (target == null) continue;

            Vector3 hitPoint = hit.ClosestPoint(worldCenter);
            target.TakeDamage(meleeDamage, hitPoint);
            Debug.Log($"[RusherAttack] {side} swipe hit '{hit.name}' for {meleeDamage} dmg.");
            return;  // one target per swing
        }

        Debug.Log($"[RusherAttack] {side} swipe missed.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns component-wise absolute value of a Vector3.
    /// Required to avoid negative half-extents from negative scale values.
    /// </summary>
    private static Vector3 AbsScale(Vector3 v) =>
        new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // ─── Scene gizmos ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawHitboxGizmo(rightFistBox, new Color(1f, 0.4f, 0.1f, 0.5f), "R");
        DrawHitboxGizmo(leftFistBox,  new Color(0.1f, 0.6f, 1f, 0.5f), "L");
    }

    private static void DrawHitboxGizmo(BoxCollider box, Color color, string label)
    {
        if (box == null) return;
        Gizmos.color = color;
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            box.transform.TransformPoint(box.center),
            box.transform.rotation,
            box.transform.lossyScale);
        Gizmos.DrawWireCube(Vector3.zero, box.size);
        Gizmos.matrix = prev;
        UnityEditor.Handles.Label(box.transform.TransformPoint(box.center), label);
    }
#endif
}
