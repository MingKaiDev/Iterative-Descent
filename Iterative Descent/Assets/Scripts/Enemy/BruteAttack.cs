using UnityEngine;
using System.Collections;

/// <summary>
/// Melee attack component for EnemyBrute. Owns three distinct attacks:
///
///   1. Combo A -- "Left+Right swipe": Mutant Swipe (left hand) -> Mutant Swipe (2) (right hand)
///   2. Combo B -- "Right+Left swipe": Mutant Swipe (1) (right hand) -> Mutant Swipe (3) (left hand)
///   3. Grab    -- "Thrusting Attack": grabs the player on contact (movement frozen),
///                  then deals a fixed chunk of damage (grabDamage) at the end of the
///                  animation -- see "Grab redesign" below.
///
/// Every decision (see TryAttack) rolls independently between Grab and a swipe combo,
/// gated by two SEPARATE cooldowns -- Grab has its own long cooldown plus a low base
/// chance so it does not spam, per design: "used sparingly, or when the player is low
/// in health" -- the chance to attempt Grab rises when PlayerHealth is below
/// playerLowHealthThreshold, but is NOT guaranteed even then (weighted, not forced).
/// A missed Grab still consumes its full cooldown, same as a landed one -- there is no
/// instant-retry, which is itself part of what keeps it rare.
///
/// Combo A vs Combo B is chosen by a coin flip each time (not alternated) -- the Brute
/// can repeat the same combo back-to-back.
///
/// Hit detection follows the exact pattern already used by EnemyAttack.cs and
/// RusherAttack.cs in this project: one-shot Physics.OverlapBox at a BoxCollider
/// hitbox's current world position, fired either by an Animation Event (frame-accurate)
/// or a coroutine fallback timed by an Inspector delay (hitboxDelay-style fields below).
/// Both paths funnel through the same private RunHitCheck() so there is only one place
/// that actually applies damage for a swipe hit. Grab is the one exception -- see "Grab
/// redesign" below -- it polls a WINDOW rather than a single instant, and its damage
/// is deferred to the end of the animation, not the moment of contact.
///
/// ─── Fist / grab hitbox setup (mirrors RusherAttack) ──────────────────────
///   Root (EnemyBrute + BruteAttack + NavMeshAgent)
///   └── CharacterRig (Animator)
///       └── ... Hand_R bone
///           └── RightFistHitbox  (BoxCollider, Is Trigger = true, NEVER enabled at
///                                 runtime -- position/size reference only)
///                                                  ↑ assign to rightFistBox
///       └── ... Hand_L bone
///           └── LeftFistHitbox   same setup       ↑ assign to leftFistBox
///       └── (centred on chest/reach, e.g. a child of the spine or hips bone)
///           └── GrabHitbox       same setup       ↑ assign to grabHitbox
///                                 (kept for gizmo/reference only -- see "Grab hit
///                                 detection" note below; Grab's hit check no longer
///                                 reads this box)
///
/// ─── Grab hit detection (2026-09-03 fix) ─────────────────────────────────
///   Grab used to run its hit check against grabHitbox alone -- a separate, much
///   smaller box (roughly a third the height of either fist box) sitting right on the
///   Hand_R bone, effectively a clone of RightFistHitbox that was never repositioned
///   for the Thrusting Attack lunge. It essentially never overlapped the player's
///   hurtbox, so Grab was landing far less often than the swipes despite firing the
///   same OverlapBox pattern. Grab's hit check now reuses rightFistBox/leftFistBox --
///   the same boxes already proven to land hits via the swipe combos -- checking both
///   (Thrusting Attack is a two-handed lunge) and taking whichever connects first.
///
/// ─── Grab redesign -- grab-then-kill (2026-09-03) ──────────────────────────
///   Grab no longer applies damage the instant its hitbox connects. It now works as a
///   genuine grab:
///     1. grabHitboxDelay after the Grab trigger fires, the grab hitbox WINDOW opens.
///     2. While open, every Update() polls rightFistBox/leftFistBox for the player
///        (PollGrabWindow) -- the FIRST overlap grabs the player immediately: the
///        window closes right there (one grab per swing), and
///        PlayerMovement.SetGrabbed(true) freezes their WASD input (look/gravity/
///        knockback keep working). No damage yet.
///     3. If nothing overlapped, the window auto-closes after grabHitboxOpenDuration --
///        a clean miss, same as before.
///     4. grabRecoveryDuration after the window closes (grabbed or not), the animation
///        is considered over. If a grab landed, THIS is when ResolveGrabOutcome() fires --
///        it deals a flat grabDamage (default 50, tune in Inspector) rather than an
///        instant kill -- and releases the movement lock -- "damage at the end of the
///        animation", not at the moment of contact. A missed window just clears normally.
///   OnDisable() releases any grab still in progress (movement unlocked, no kill) so a
///   Brute destroyed mid-grab (e.g. shot dead while holding the player) can never leave
///   the player permanently frozen -- see ReleaseGrabIfActive().
///
/// ─── Animator parameters (Triggers) ────────────────────────────────────────
///   "SwipeA_Hit1"  -- Mutant Swipe        (Combo A, hit 1 -- left hand)
///   "SwipeA_Hit2"  -- Mutant Swipe (2)    (Combo A, hit 2 -- right hand)
///   "SwipeB_Hit1"  -- Mutant Swipe (1)    (Combo B, hit 1 -- right hand)
///   "SwipeB_Hit2"  -- Mutant Swipe (3)    (Combo B, hit 2 -- left hand)
///   "Grab"         -- Thrusting Attack
///
///   IMPORTANT -- see project-enemy-system.md 2026-08-10 "alternating punch animations"
///   bug: wire every one of these five triggers as Any State -> [clip state], NOT from
///   a single locomotion state. The agent stops moving the instant it enters attack
///   range (see EnemyBrute.StopAgent), so if a trigger is only wired from "Walking" it
///   can arrive after the Animator has already fallen back to Idle and sit unconsumed
///   forever -- this exact bug froze CyberSoldier's first punch wiring attempt.
///
/// ─── Animation Events (optional but recommended) ──────────────────────────
///   Impact frame of Mutant Swipe / (1)   -> OnComboHit1Frame()
///   Impact frame of Mutant Swipe (2)/(3) -> OnComboHit2Frame()
///   Reach begins (Thrusting Attack)      -> OnGrabHitboxOpen()
///   Reach ends (Thrusting Attack)        -> OnGrabHitboxClose()
///   Last frame of any clip (optional)    -> OnAttackEnd()
///   All are optional -- the coroutine fallback (the *Delay/*Duration fields below)
///   handles hit detection, the grab window, and state reset regardless of whether
///   events are wired, same as EnemyAttack/RusherAttack. Watch the "[BruteAttack] HIT
///   CHECK ACTIVE" / "GRAB HITBOX OPEN/CLOSE" logs against the swing in Play mode to
///   tune the *Delay/*Duration fields if you are not using events. OnAttackEnd() also
///   resolves (deals grabDamage to) a still-active grab if it fires before the coroutine
///   reaches its own natural end, so wiring it early never soft-locks the player.
/// </summary>
public class BruteAttack : MonoBehaviour
{
    // ─── Inspector -- Swipe Combos ─────────────────────────────────────────────

    [Header("Swipe Combo Settings")]
    [Tooltip("Seconds of cooldown after a full 2-hit swipe combo finishes before another " +
             "swipe or grab decision can fire. Does not affect Grab's own separate cooldown.")]
    [SerializeField] private float attackCooldown = 1.8f;
    [Tooltip("Damage per swipe hit (both hits of either combo use this same value).")]
    [SerializeField] private float meleeDamage = 18f;
    [Header("Combo A -- Left+Right Swipe (Mutant Swipe -> Mutant Swipe (2))")]
    [SerializeField] private string comboAHit1TriggerName = "SwipeA_Hit1";
    [Tooltip("Seconds after SwipeA_Hit1 fires before the hit check runs (fallback if no " +
             "Animation Event calls OnComboHit1Frame). Tune against the Mutant Swipe clip.")]
    [SerializeField] private float comboAHit1Delay = 1.43f;  // frame 43/80 @ 30fps, ASSUMED same as Combo B (identical 80-frame clip length + identical 0.90625 exit time) -- not yet visually confirmed on Mutant Swiping.fbx itself, see project-enemy-system.md
    [Tooltip("Seconds from SwipeA_Hit1 firing until SwipeA_Hit2 fires -- NOT just a short " +
             "beat, this must cover the REST of the Mutant Swipe clip after its hitbox " +
             "point, or SwipeA_Hit2 interrupts Mutant Swipe mid-animation (Any State " +
             "transitions can cut off the current clip the instant the trigger fires). " +
             "Tune as: (time from SwipeA_Hit1 firing to Mutant Swipe visually finishing) " +
             "- comboAHit1Delay. Kept separate from Combo B's gap since Mutant Swipe (1) " +
             "is very unlikely to be the same length as Mutant Swipe.")]
    [SerializeField] private float comboAGapDuration = 0.98f;  // (exit-time frame 72.5 - hit1 frame 43) / 30fps -- same assumption as comboAHit1Delay above
    [SerializeField] private string comboAHit2TriggerName = "SwipeA_Hit2";
    [Tooltip("Seconds after SwipeA_Hit2 fires before the hit check runs. Tune against the " +
             "Mutant Swipe (2) clip -- likely a different length to Hit 1, do not assume " +
             "they share a delay.")]
    [SerializeField] private float comboAHit2Delay = 0.9f;  // ESTIMATE: frame 27/50 @ 30fps -- extrapolated from Combo B Hit1's confirmed 53.75% impact fraction (frame 43/80) applied to the 50-frame Hit2 clip, NOT independently confirmed. Exit-time for this state is 0.85 (read from Phobos.controller "Mutant Swiping 2"), lower than Hit1's 0.90625, so the clip's pacing may differ -- treat this as a better starting guess than the old 0.4s, not final data.
    [Tooltip("Seconds AFTER the Hit2 hit-check fires before the combo is considered " +
             "over (_isAttacking cleared). Must cover the rest of Mutant Swipe (2) after " +
             "its hit frame, or EnemyBrute can resume chasing / fire a new attack trigger " +
             "while Hit2 is still visibly playing, cutting it short. Added 2026-08-29 -- " +
             "see grabRecoveryDuration below for the same bug found on Grab.")]
    [SerializeField] private float comboAHit2RecoveryDuration = 0.52f;  // ESTIMATE: (exit-time frame 42.5 - hit frame 27) / 30fps, exit-time 0.85 read from Phobos.controller "Mutant Swiping 2"

    [Header("Combo B -- Right+Left Swipe (Mutant Swipe (1) -> Mutant Swipe (3))")]
    [SerializeField] private string comboBHit1TriggerName = "SwipeB_Hit1";
    [Tooltip("Seconds after SwipeB_Hit1 fires before the hit check runs. Tune against the " +
             "Mutant Swipe (1) clip.")]
    [SerializeField] private float comboBHit1Delay = 1.43f;  // frame 43 of 80 @ 30fps, confirmed by user directly in the Mutant Swipe (1) Animation import inspector 2026-08-29
    [Tooltip("Same purpose as comboAGapDuration, for Combo B -- seconds from SwipeB_Hit1 " +
             "firing until SwipeB_Hit2 fires. Tune as: (time from SwipeB_Hit1 firing to " +
             "Mutant Swipe (1) visually finishing) - comboBHit1Delay.")]
    [SerializeField] private float comboBGapDuration = 0.98f;  // (exit-time frame 72.5 - hit1 frame 43) / 30fps -- exit-time 0.90625 read directly from Phobos.controller's "Mutant Swipe 1" return transition
    [SerializeField] private string comboBHit2TriggerName = "SwipeB_Hit2";
    [Tooltip("Seconds after SwipeB_Hit2 fires before the hit check runs. Tune against the " +
             "Mutant Swipe (3) clip.")]
    [SerializeField] private float comboBHit2Delay = 0.9f;  // ESTIMATE: frame 27/50 @ 30fps -- same extrapolation and same caveats as comboAHit2Delay above ("Mutant Swiping 3" also has exit-time 0.85 in the controller). Confirm with a real hit frame the same way Combo B Hit1's frame 43 was found (scrub the clip) before trusting this in a build.
    [Tooltip("Same purpose as comboAHit2RecoveryDuration, for Combo B.")]
    [SerializeField] private float comboBHit2RecoveryDuration = 0.52f;  // ESTIMATE: (exit-time frame 42.5 - hit frame 27) / 30fps, exit-time 0.85 read from Phobos.controller "Mutant Swiping 3"

    // ─── Inspector -- Grab (instant kill) ──────────────────────────────────────

    [Header("Grab Attack (Thrusting Attack) -- fixed damage, tune carefully")]
    [SerializeField] private string grabTriggerName = "Grab";
    [Tooltip("Seconds after the Grab trigger fires before the grab hitbox WINDOW opens " +
             "(2026-09-03: was a single hit-check instant, now the start of a window -- " +
             "see grabHitboxOpenDuration). CONFIRMED 2026-09-03 against Thrust Slash.fbx's " +
             "own baked bone curves (frame 53/101) -- see grabHitboxOpenDuration's tooltip " +
             "for how. OnGrabHitboxOpen is now wired as a real Animation Event at this same " +
             "frame, so this value is a fallback that should almost never be the one that " +
             "actually opens the window during normal play.")]
    [SerializeField] private float grabHitboxDelay = 1.7667f;  // CONFIRMED: frame 53/101 @ 30fps -- both hand bones (mixamorig:LeftHand / mixamorig:RightHand) sampled via forward-kinematics across every baked frame of Thrust Slash.fbx; frame 53 is where both hands' distance from mixamorig:Hips first crosses 90% of that hand's own peak reach SIMULTANEOUSLY (frames 15-20 hit that threshold for one hand alone but not both together -- that's mid wind-up, not the real thrust). Superseded the old 1.8s swipe-derived estimate, which turned out to be close (1.8 vs 1.767) but was never actually measured against this clip.
    [Tooltip("Seconds the grab hitbox WINDOW stays open once it opens. While open, every " +
             "Update() polls rightFistBox/leftFistBox for the player (PollGrabWindow) -- " +
             "the first overlap grabs the player immediately (movement locked, window " +
             "closes early) rather than waiting out the rest of this duration. CONFIRMED " +
             "2026-09-03: frames 53-63/101 (11 frames = both hands held near-maximum " +
             "extension from the hips simultaneously -- a genuine sustained-reach plateau " +
             "in the clip, not an instant). OnGrabHitboxClose is wired as a real Animation " +
             "Event at frame 64, so this is the fallback duration. Widen this if Grab keeps " +
             "whiffing despite the Brute visibly reaching the player; narrow it if Grab " +
             "connects from further away than the animation looks like it should.")]
    [SerializeField] private float grabHitboxOpenDuration = 0.3667f;
    [Tooltip("Seconds AFTER the grab hitbox WINDOW CLOSES before the attack is considered " +
             "fully over (_isAttacking cleared). Covers the rest of the Thrusting Attack " +
             "clip so EnemyBrute doesn't resume chasing / fire a new trigger mid-animation " +
             "(FIX 2026-08-29, same root cause as comboXHit2RecoveryDuration above). " +
             "2026-09-03: if the window landed a grab, THIS is also when the instant kill " +
             "actually resolves -- see ResolveGrabOutcome() -- so the player stays " +
             "grabbed (frozen, alive) for this entire stretch, not just an instant. " +
             "CONFIRMED 2026-09-03: (OnAnimEnd frame 100.21 - window-close frame 64) / 30fps " +
             "-- OnAnimEnd's 0.9921875 normalized time was already wired; window-close frame " +
             "is the same confirmed frame 64 from grabHitboxOpenDuration above.")]
    [SerializeField] private float grabRecoveryDuration = 1.2071f;  // CONFIRMED: (100.2109 - 64) / 30fps -- see tooltip
    [Tooltip("Seconds before Grab can be attempted again, win or miss. This is the main " +
             "lever for keeping the instant kill rare -- a miss still burns the full " +
             "cooldown, there is no instant retry.")]
    [SerializeField] private float grabCooldown = 16f;
    [Tooltip("Base chance [0-1] to attempt Grab instead of a swipe combo on any decision " +
             "tick where Grab is off cooldown, used while the player is ABOVE " +
             "playerLowHealthThreshold. Keep this low -- it is the 'sparingly' half of the " +
             "design brief.")]
    [Range(0f, 1f)]
    [SerializeField] private float grabChanceBase = 0.12f;
    [Tooltip("Chance [0-1] to attempt Grab instead of a swipe combo once the player's HP " +
             "fraction drops to or below playerLowHealthThreshold. Weighted, not guaranteed " +
             "-- the player can still survive a few close calls, by design.")]
    [Range(0f, 1f)]
    [SerializeField] private float grabChanceLowHealth = 0.7f;
    [Tooltip("Player HP fraction (CurrentHealth / maxHealth) at or below which " +
             "grabChanceLowHealth applies instead of grabChanceBase. 0.3 = 30% HP.")]
    [Range(0f, 1f)]
    [SerializeField] private float playerLowHealthThreshold = 0.3f;
    [Tooltip("Flat damage dealt to the player when a Grab resolves at the end of the " +
             "animation. No longer an instant kill -- tune this like any other damage " +
             "value. Not affected by ApplyDamageMultiplier (see that method's doc).")]
    [SerializeField] private float grabDamage = 50f;

    // ─── Inspector -- Hitboxes ──────────────────────────────────────────────────

    [Header("Hitboxes -- sizing reference only, never enabled at runtime")]
    [Tooltip("BoxCollider on the left hand bone. Used for Combo A hit 1 and Combo B hit 2.")]
    [SerializeField] private BoxCollider leftFistBox;
    [Tooltip("BoxCollider on the right hand bone. Used for Combo A hit 2 and Combo B hit 1.")]
    [SerializeField] private BoxCollider rightFistBox;
    [Tooltip("Legacy reference-only box for the Grab attack's gizmo. NOT read by the hit " +
             "check anymore (2026-09-03) -- it was a separate, badly-placed box that " +
             "rarely overlapped the player, so Grab now reuses rightFistBox/leftFistBox " +
             "instead (see the class doc comment's \"Grab hit detection\" note). Safe to " +
             "leave assigned or clear.")]
    [SerializeField] private BoxCollider grabHitbox;

    [Header("Hit Detection")]
    [Tooltip("Layers the OverlapBox checks hit. Set this to the Player layer.")]
    [SerializeField] private LayerMask hitableLayers = ~0;

    [Header("Knockback (swipe hits only -- Grab freezes+damages, no knockback needed)")]
    [Tooltip("Horizontal speed (m/s) given to the player, pushed straight away from the " +
             "Brute, the instant a swipe connects. This is hand-off to " +
             "PlayerMovement.ApplyKnockback -- that component owns the actual decay, see " +
             "its knockbackRecoverySpeed field.")]
    [SerializeField] private float knockbackForce = 6f;
    [Tooltip("Small upward component added on top of the horizontal push, purely for a bit " +
             "of stagger feel (a hit that only shoves sideways can look like the player " +
             "slid on ice). 0 = perfectly horizontal.")]
    [SerializeField] private float knockbackUpwardKick = 1f;

    [Header("Grab Feedback")]
    [Tooltip("Seconds the camera-shake pulse (PlayerMovement.Shake) lasts, fired the " +
             "instant Grab connects -- see GrabPlayer(). Linear falloff over this duration.")]
    [SerializeField] private float grabShakeDuration = 0.35f;
    [Tooltip("Peak magnitude (metres) of that camera-shake pulse. PlayerMovement applies " +
             "it as a small additive offset to the camera's local position, so keep this " +
             "small -- 0.15-0.25 reads as a hard jolt without the view breaking apart.")]
    [SerializeField] private float grabShakeMagnitude = 0.18f;

    [Header("Animator -- leave empty, auto-found in children")]
    [SerializeField] private Animator animator;

    // ─── Cached hashes ────────────────────────────────────────────────────────

    private int _comboAHit1Hash;
    private int _comboAHit2Hash;
    private int _comboBHit1Hash;
    private int _comboBHit2Hash;
    private int _grabHash;

    // ─── Private state ────────────────────────────────────────────────────────

    private float   _baseMeleeDamage;   // original inspector value, never changes after Awake
    private float   _swipeCooldownTimer;
    private float   _grabCooldownTimer;
    private bool    _isAttacking;
    private bool    _currentComboIsA;   // which box mapping the in-progress combo is using
    private bool    _hitCheckDone;      // guards against double-check (event fired + coroutine fallback) -- swipes only, see Grab redesign note above
    private bool    _hitboxWindowOpen;  // true only while Grab's hitbox window is open -- polled every Update()
    private PlayerHealth _grabbedTarget; // non-null only while a landed Grab is holding the player -- cleared on kill/release
    private Coroutine _attackRoutine;
    private PlayerHealth _player;
    private PlayerMovement _playerMovement;
    private BruteAudio _audio; // null-safe; optional component, same convention as EnemyBrute's _audio

    // ─── Public Read-Only State ─────────────────────────────────────────────────

    /// <summary>
    /// True while a swipe combo or Grab is currently mid-swing (from the moment
    /// StartComboSwipe/StartGrab fires the Animator trigger until the coroutine
    /// finishes, whether or not it landed). EnemyBrute reads this so a player
    /// leaving range mid-attack doesn't yank the agent back into a chase while
    /// the animation is still playing -- see EnemyBrute.TickAttack().
    /// </summary>
    public bool IsAttacking => _isAttacking;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        _audio = GetComponent<BruteAudio>(); // null-safe; optional component

        // BoxColliders are never enabled at runtime -- sizing references only.
        if (leftFistBox  != null) leftFistBox.enabled  = false;
        if (rightFistBox != null) rightFistBox.enabled = false;
        if (grabHitbox   != null) grabHitbox.enabled   = false;

        _baseMeleeDamage = meleeDamage;

        _comboAHit1Hash = Animator.StringToHash(comboAHit1TriggerName);
        _comboAHit2Hash = Animator.StringToHash(comboAHit2TriggerName);
        _comboBHit1Hash = Animator.StringToHash(comboBHit1TriggerName);
        _comboBHit2Hash = Animator.StringToHash(comboBHit2TriggerName);
        _grabHash       = Animator.StringToHash(grabTriggerName);

        // Same resolution convention as BossAttackRegistry/BossStateMachine --
        // cached once, since the player does not change mid-scene.
        _player = FindFirstObjectByType<PlayerHealth>();
        if (_player == null)
            Debug.LogWarning("[BruteAttack] No PlayerHealth found in scene -- Grab health " +
                              "weighting will always use the base chance.");

        _playerMovement = _player != null ? _player.GetComponent<PlayerMovement>() : null;
        if (_player != null && _playerMovement == null)
            Debug.LogWarning("[BruteAttack] PlayerHealth found but no PlayerMovement on the " +
                              "same GameObject -- swipe knockback will be skipped.");
    }

    private void Update()
    {
        if (_swipeCooldownTimer > 0f) _swipeCooldownTimer -= Time.deltaTime;
        if (_grabCooldownTimer  > 0f) _grabCooldownTimer  -= Time.deltaTime;

        // Poll the grab hitbox window every frame it's open -- see PollGrabWindow's doc
        // comment. Stops polling itself the instant a grab lands (GrabPlayer clears
        // _hitboxWindowOpen), so this is cheap outside of Grab's brief open window.
        if (_hitboxWindowOpen)
            PollGrabWindow();
    }

    private void OnDisable()
    {
        // Safety net: if this Brute is disabled/destroyed while it's holding the player
        // (e.g. shot dead mid-grab), the GrabSequence coroutine is killed by Unity before
        // it ever reaches ResolveGrabOutcome(). Without this, the player would stay
        // movement-locked forever with no kill and no way to recover. Releases with no
        // kill -- dying mid-grab is a reasonable way for the player to escape it.
        ReleaseGrabIfActive();
    }

    // ─── DDA API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a DDA damage multiplier relative to the base inspector value, same
    /// convention as EnemyAttack/RusherAttack. Deliberately does NOT affect Grab --
    /// grabDamage is a flat, separately-tunable value regardless of tier; DDA should
    /// instead tune how OFTEN Grab can happen via grabCooldown/grabChance* if that is
    /// ever needed per-tier (not wired yet -- flag for EnemyDirector if wanted).
    /// </summary>
    public void ApplyDamageMultiplier(float multiplier)
    {
        meleeDamage = _baseMeleeDamage * multiplier;
        Debug.Log($"[BruteAttack] Swipe damage set to {meleeDamage:F1} " +
                  $"(base {_baseMeleeDamage:F1} x{multiplier:F2}). Grab damage unaffected ({grabDamage:F0}).");
    }

    // ─── Public API ── called by EnemyBrute every frame in attack range ───────

    /// <summary>
    /// Attempt to start an attack. Handles both cooldowns and the Grab-vs-swipe
    /// decision internally. Call continuously while in melee range.
    /// </summary>
    public void TryAttack()
    {
        if (_isAttacking) return;

        bool grabAvailable  = _grabCooldownTimer  <= 0f;
        bool swipeAvailable = _swipeCooldownTimer <= 0f;
        if (!grabAvailable && !swipeAvailable) return;

        if (grabAvailable && ShouldAttemptGrab())
        {
            StartGrab();
            return;
        }

        if (swipeAvailable)
            StartComboSwipe();
    }

    /// <summary>
    /// Rolls whether this decision tick should be a Grab attempt instead of a swipe.
    /// Weighted by the player's current HP fraction -- see grabChanceBase /
    /// grabChanceLowHealth / playerLowHealthThreshold tooltips. Never guarantees Grab,
    /// by design (see class doc comment).
    /// </summary>
    private bool ShouldAttemptGrab()
    {
        float chance = grabChanceBase;
        string tier = "base";

        if (_player != null && _player.maxHealth > 0f)
        {
            float healthFraction = _player.CurrentHealth / _player.maxHealth;
            if (healthFraction <= playerLowHealthThreshold)
            {
                chance = grabChanceLowHealth;
                tier = "LOW HEALTH";
            }
        }

        float roll    = UnityEngine.Random.value;
        bool  success = roll < chance;

        // Logs EVERY decision, pass or fail -- added 2026-08-29 because "the swipes are
        // denying the grab" is impossible to diagnose from outside without seeing the
        // actual rolls. If this never prints "GRAB" at all despite firing every
        // ~attackCooldown seconds, the roll is genuinely just losing (expected ~88% of
        // the time at the base 12% chance) -- that's balance, not a bug. If it prints
        // "GRAB" but StartGrab() never visibly plays, the bug is downstream of this
        // check (Animator wiring), not the decision itself.
        Debug.Log($"[BruteAttack] Grab roll ({tier}) -- {roll:F3} vs chance {chance:F2} " +
                  $"-> {(success ? "GRAB" : "swipe")}");

        return success;
    }

    // ─── Core attack flow -- swipe combos ──────────────────────────────────────

    private void StartComboSwipe()
    {
        _isAttacking = true;

        // _swipeCooldownTimer is NOT set here -- see ComboSequence()'s end. Setting it at
        // the start (the old behaviour) meant the cooldown had already run out before the
        // combo animation even finished playing (attackCooldown is shorter than a full
        // combo), so TryAttack() could fire again the instant _isAttacking cleared --
        // zero gap, endless back-to-back attacking, the Brute never breaking off to walk.
        // Fixed 2026-08-29.

        // Coin flip, not alternation -- the Brute can repeat the same combo twice in a row.
        bool isComboA = UnityEngine.Random.value < 0.5f;

        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(ComboSequence(isComboA));

        Debug.Log($"[BruteAttack] Starting {(isComboA ? "Left+Right" : "Right+Left")} swipe combo.");
    }

    private IEnumerator ComboSequence(bool isComboA)
    {
        _currentComboIsA = isComboA;

        // ── Hit 1 ──
        _hitCheckDone = false;
        if (animator != null)
            animator.SetTrigger(isComboA ? _comboAHit1Hash : _comboBHit1Hash);

        float delay1 = isComboA ? comboAHit1Delay : comboBHit1Delay;
        yield return new WaitForSeconds(delay1);

        if (!_hitCheckDone)
        {
            RunHitCheck(
                isComboA ? leftFistBox : rightFistBox,
                isComboA ? "Combo A Hit 1 (Mutant Swipe)" : "Combo B Hit 1 (Mutant Swipe (1))",
                instaKill: false);
        }

        float gap = isComboA ? comboAGapDuration : comboBGapDuration;
        yield return new WaitForSeconds(gap);

        // ── Hit 2 ──
        _hitCheckDone = false;
        if (animator != null)
            animator.SetTrigger(isComboA ? _comboAHit2Hash : _comboBHit2Hash);

        float delay2 = isComboA ? comboAHit2Delay : comboBHit2Delay;
        yield return new WaitForSeconds(delay2);

        if (!_hitCheckDone)
        {
            RunHitCheck(
                isComboA ? rightFistBox : leftFistBox,
                isComboA ? "Combo A Hit 2 (Mutant Swipe (2))" : "Combo B Hit 2 (Mutant Swipe (3))",
                instaKill: false);
        }

        // Don't release _isAttacking the instant the hit check fires -- Hit2's clip is
        // still playing for a bit after its hit frame. Releasing early let EnemyBrute
        // resume chasing / fire a new attack trigger mid-clip, visibly cutting the swipe
        // short (same root cause as the Grab "cut off" bug below). Added 2026-08-29.
        float recovery2 = isComboA ? comboAHit2RecoveryDuration : comboBHit2RecoveryDuration;
        yield return new WaitForSeconds(recovery2);

        // FIX for "never walks anymore after attacking" (2026-08-29): the cooldown now
        // starts counting from here, when the combo has actually finished, not from
        // StartComboSwipe() at the top. This is what gives attackCooldown seconds of real
        // breathing room between combos instead of none.
        _swipeCooldownTimer = attackCooldown;

        _isAttacking   = false;
        _attackRoutine = null;
    }

    // ─── Core attack flow -- grab ───────────────────────────────────────────────

    private void StartGrab()
    {
        _isAttacking      = true;
        _hitboxWindowOpen = false; // opened later, either by the coroutine delay or an Animation Event
        _grabbedTarget    = null;

        // _grabCooldownTimer is NOT set here -- see GrabSequence()'s end. Same reasoning
        // as StartComboSwipe() above: setting it at the start under-counts the real
        // post-Grab downtime by however long the Thrusting Attack clip takes to finish
        // playing. Less visible here since grabCooldown (16s) is much longer than the
        // clip, but fixed for consistency and so the tuned 16s value means what it says.

        if (animator != null)
            animator.SetTrigger(_grabHash);

        if (_attackRoutine != null) StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(GrabSequence());

        Debug.Log("[BruteAttack] Starting GRAB attack (grab-then-kill).");
    }

    private IEnumerator GrabSequence()
    {
        // -- Window open --
        yield return new WaitForSeconds(grabHitboxDelay);
        OpenGrabHitboxWindow(); // idempotent -- harmless if an Animation Event already opened it

        // -- Window stays open for grabHitboxOpenDuration, or until PollGrabWindow (in
        // Update()) grabs the player and closes it early --
        yield return new WaitForSeconds(grabHitboxOpenDuration);
        CloseGrabHitboxWindow(); // idempotent -- harmless if already closed by a landed grab

        // FIX for "grab keeps getting cut off" (2026-08-29): don't clear _isAttacking the
        // instant the window closes -- EnemyBrute would resume chasing / fire a new attack
        // trigger while the Thrusting Attack clip still had time left to play, and Any
        // State attack transitions have 0 Transition Duration so the new trigger would cut
        // the current clip instantly instead of blending. Wait out the clip's real
        // remaining tail before releasing the attack lock.
        yield return new WaitForSeconds(grabRecoveryDuration);

        // 2026-09-03: this is "the end of the animation" -- if the window above landed a
        // grab, the instant kill fires HERE, not at the moment of contact. No-op if
        // nothing was grabbed (a clean miss).
        ResolveGrabOutcome();

        // Same fix as ComboSequence() above -- cooldown starts counting from the real end
        // of the attack, not from StartGrab() at the top.
        _grabCooldownTimer = grabCooldown;

        _isAttacking   = false;
        _attackRoutine = null;
    }

    // ─── Animation Event receivers -- TUNING TELEMETRY (added 2026-08-29) ─────
    //
    // Purely diagnostic -- log-only, no gameplay effect, safe to place anywhere on any
    // clip without breaking anything. Add one Animation Event calling OnAnimStart at
    // frame 0 and one calling OnAnimEnd at the clip's last frame, on EACH of the six
    // attack clips (Scream, Mutant Swipe, Mutant Swipe (2), Mutant Swipe (1),
    // Mutant Swipe (3), Thrusting Attack). Both take a single string parameter -- type
    // the clip's own name into the event's "String" field (e.g. "Mutant Swipe (1)") so
    // the console log is unambiguous regardless of which combo is playing.
    //
    // With these wired, a single swing in Play mode prints three real timestamps --
    // ANIM START, HIT CHECK ACTIVE (already existed), ANIM END -- so comboXHit1Delay /
    // comboXGapDuration / comboXHit2Delay can be read directly off Time.time deltas
    // instead of computed from raw clip frame data (which doesn't account for Any State
    // transition blending, Animator playback speed, or anything else that only shows up
    // at runtime). This replaces the frame-math approach used for the current
    // comboBHit1Delay/comboAHit1Delay values -- once you have real START/END timestamps,
    // send me the deltas and I'll recompute every *Delay/*Gap field from those instead.

    /// <summary>[Animation Event, diagnostic only] Place at frame 0 of any attack clip.</summary>
    public void OnAnimStart(string clipLabel)
    {
        Debug.Log($"[BruteAttack] ANIM START -- {clipLabel} | time={Time.time:F3}");
    }

    /// <summary>[Animation Event, diagnostic only] Place at the last frame of any attack clip.</summary>
    public void OnAnimEnd(string clipLabel)
    {
        Debug.Log($"[BruteAttack] ANIM END -- {clipLabel} | time={Time.time:F3}");
    }

    // ─── Animation Event receivers (OPTIONAL -- system works without them) ────

    /// <summary>[Animation Event] Impact frame of Mutant Swipe / Mutant Swipe (1).</summary>
    public void OnComboHit1Frame()
    {
        if (_hitCheckDone) return;
        RunHitCheck(
            _currentComboIsA ? leftFistBox : rightFistBox,
            _currentComboIsA ? "Combo A Hit 1 (Mutant Swipe)" : "Combo B Hit 1 (Mutant Swipe (1))",
            instaKill: false);
    }

    /// <summary>[Animation Event] Impact frame of Mutant Swipe (2) / Mutant Swipe (3).</summary>
    public void OnComboHit2Frame()
    {
        if (_hitCheckDone) return;
        RunHitCheck(
            _currentComboIsA ? rightFistBox : leftFistBox,
            _currentComboIsA ? "Combo A Hit 2 (Mutant Swipe (2))" : "Combo B Hit 2 (Mutant Swipe (3))",
            instaKill: false);
    }

    /// <summary>[Animation Event -- WIRED 2026-09-03] Fires from Thrust Slash.fbx at
    /// normalized time 0.5247525 (frame 53/101), the confirmed frame where both hand
    /// bones are simultaneously near their maximum reach from the hips -- opens the grab
    /// hitbox window frame-accurately. The coroutine fallback (grabHitboxDelay) also opens
    /// it regardless, in case the Animator isn't actually playing this clip for some
    /// reason -- whichever fires first wins, this is idempotent either way.</summary>
    public void OnGrabHitboxOpen() => OpenGrabHitboxWindow();

    /// <summary>[Animation Event -- WIRED 2026-09-03] Fires from Thrust Slash.fbx at
    /// normalized time 0.6336634 (frame 64/101), the confirmed frame where both hands'
    /// reach drops back off the plateau -- closes the grab hitbox window frame-accurately.
    /// The coroutine fallback (grabHitboxOpenDuration after the open) also closes it
    /// regardless. Safe to call even if the window is already closed, e.g. because a grab
    /// already landed (idempotent).</summary>
    public void OnGrabHitboxClose() => CloseGrabHitboxWindow();

    /// <summary>
    /// [Optional Animation Event] Place on the last frame of any attack clip to reset
    /// state early. The coroutine fallback handles this regardless of whether it's wired.
    /// Also resolves (instant-kills) a still-active Grab if this fires before
    /// GrabSequence() reaches its own natural end -- without this, wiring OnAttackEnd on
    /// the Thrusting Attack clip could StopCoroutine the grab away with the player still
    /// frozen and never killed.
    /// </summary>
    public void OnAttackEnd()
    {
        ResolveGrabOutcome(); // no-op unless a grab is actually in progress
        _isAttacking = false;
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    // ─── Grab window (2026-09-03) ──────────────────────────────────────────────

    private void OpenGrabHitboxWindow()
    {
        if (_hitboxWindowOpen) return; // idempotent -- event + coroutine fallback can both fire
        _hitboxWindowOpen = true;
        Debug.Log($"[BruteAttack] GRAB HITBOX OPEN | time={Time.time:F2}");
    }

    private void CloseGrabHitboxWindow()
    {
        if (!_hitboxWindowOpen) return; // idempotent -- also true once GrabPlayer already closed it
        _hitboxWindowOpen = false;
        Debug.Log($"[BruteAttack] GRAB HITBOX CLOSE | time={Time.time:F2}");
    }

    /// <summary>
    /// Called every Update() while the grab hitbox window is open. Checks
    /// rightFistBox/leftFistBox for the player exactly like a swipe's hit check, but as a
    /// GRAB rather than an instant kill -- see GrabPlayer(). Stops itself the moment
    /// something is grabbed (_hitboxWindowOpen is cleared by GrabPlayer), so this never
    /// grabs more than one target per swing even though it polls every frame the window
    /// is open (a window can span several frames, unlike the old single-instant check).
    /// </summary>
    private void PollGrabWindow()
    {
        foreach (BoxCollider box in new[] { rightFistBox, leftFistBox })
        {
            if (box == null) continue;

            PlayerHealth target = FindPlayerOverlap(box, out _);
            if (target == null) continue;

            GrabPlayer(target);
            return;
        }
    }

    /// <summary>
    /// The grab hitbox window found the player. Locks their movement immediately via
    /// PlayerMovement.SetGrabbed(true) -- no damage yet, grabDamage is dealt via
    /// ResolveGrabOutcome() at the end of the animation (see GrabSequence()). Closes the
    /// window early so only one target can ever be grabbed per swing.
    /// </summary>
    private void GrabPlayer(PlayerHealth target)
    {
        _grabbedTarget    = target;
        _hitboxWindowOpen = false;

        PlayerMovement pm = target.GetComponent<PlayerMovement>();
        if (pm == null) pm = _playerMovement; // fallback to the cached reference from Awake

        if (pm != null)
        {
            // transform = this Brute -- forces the player's camera to stay locked onto
            // whatever is holding them (see PlayerMovement.UpdateGrabLook) instead of
            // free-looking away from their own death. Cleared automatically the moment
            // SetGrabbed(false) is called (ResolveGrabOutcome/ReleaseGrabIfActive).
            pm.SetGrabbed(true, transform);
            pm.Shake(grabShakeDuration, grabShakeMagnitude);
        }
        else
        {
            Debug.LogWarning($"[BruteAttack] Grab connected on '{target.name}' but no " +
                              "PlayerMovement was found to freeze -- player will not be " +
                              "movement-locked, but grabDamage will still be dealt " +
                              "at the end of the animation.");
        }

        _audio?.PlayGrabConnect();

        Debug.Log($"[BruteAttack] GRAB CONNECTED on '{target.name}' -- movement locked, " +
                  "camera forced, grabDamage resolves at the end of the animation.");
    }

    /// <summary>
    /// Called at the true end of the Grab animation (GrabSequence()'s natural finish, or
    /// early via OnAttackEnd()). If PollGrabWindow ever grabbed the player, this is where
    /// grabDamage actually lands and the movement lock is released. No-op if nothing was
    /// grabbed this swing (a clean miss) -- safe to call unconditionally.
    /// </summary>
    private void ResolveGrabOutcome()
    {
        if (_grabbedTarget == null) return;

        PlayerHealth target = _grabbedTarget;
        _grabbedTarget = null; // clear first so a re-entrant call (see OnAttackEnd) is a no-op

        PlayerMovement pm = target.GetComponent<PlayerMovement>();
        if (pm == null) pm = _playerMovement;
        pm?.SetGrabbed(false);

        _audio?.PlayNeckSnap();

        target.TakeDamage(grabDamage, target.transform.position);
        Debug.Log($"[BruteAttack] Grab (Thrusting Attack) resolved on '{target.name}' -- " +
                  $"dealt {grabDamage:F0} damage at animation end.");
    }

    /// <summary>
    /// Releases a grab in progress WITHOUT killing -- only called from OnDisable(), when
    /// this Brute is disabled/destroyed mid-grab (e.g. shot dead while holding the
    /// player) and GrabSequence()'s coroutine is killed before it can reach
    /// ResolveGrabOutcome() on its own. Prevents the player being left permanently
    /// movement-locked with no way to recover.
    /// </summary>
    private void ReleaseGrabIfActive()
    {
        if (_grabbedTarget == null) return;

        PlayerHealth target = _grabbedTarget;
        _grabbedTarget = null;

        PlayerMovement pm = target.GetComponent<PlayerMovement>();
        if (pm == null) pm = _playerMovement;
        pm?.SetGrabbed(false);

        Debug.Log($"[BruteAttack] Grab on '{target.name}' released early (BruteAttack " +
                  "disabled/destroyed mid-grab) -- movement restored, no kill applied.");
    }

    // ─── Hit check ──────────────────────────────────────────────────────────────

    /// <summary>
    /// One-shot OverlapBox at the given hitbox's current world position. Damage (or the
    /// instant kill) only registers at the exact moment this runs. When instaKill is
    /// true, damage is CurrentHealth + 1 -- guaranteed lethal regardless of maxHealth,
    /// rather than a large magic-number damage value. Thin wrapper around the multi-box
    /// overload below for the swipe combos, which only ever check one hand at a time.
    /// </summary>
    private void RunHitCheck(BoxCollider box, string label, bool instaKill)
    {
        RunHitCheck(new[] { box }, label, instaKill);
    }

    /// <summary>
    /// Same as the single-box overload, but tries each box in order and stops at the
    /// first one that actually connects -- used by Grab, which now checks both
    /// rightFistBox and leftFistBox instead of the dedicated (and badly placed)
    /// grabHitbox. See the "Grab hit detection" note in this file's header comment.
    /// Null entries in boxes are skipped, so callers don't need to null-check first.
    /// </summary>
    private void RunHitCheck(BoxCollider[] boxes, string label, bool instaKill)
    {
        _hitCheckDone = true;

        Debug.Log($"[BruteAttack] HIT CHECK ACTIVE -- {label} | time={Time.time:F2}");

        bool anyBoxAssigned = false;

        foreach (BoxCollider box in boxes)
        {
            if (box == null) continue;
            anyBoxAssigned = true;

            PlayerHealth target = FindPlayerOverlap(box, out Vector3 hitPoint);
            if (target == null) continue;

            if (instaKill)
            {
                float killAmount = target.CurrentHealth + 1f;
                target.TakeDamage(killAmount, hitPoint);
                Debug.Log($"[BruteAttack] {label} connected on '{target.name}' -- INSTANT KILL.");
            }
            else
            {
                target.TakeDamage(meleeDamage, hitPoint);
                Debug.Log($"[BruteAttack] {label} hit '{target.name}' for {meleeDamage} dmg.");
                ApplyKnockback(target.transform);
            }
            return; // one target per swing
        }

        if (!anyBoxAssigned)
        {
            Debug.Log($"[BruteAttack] STUB -- no hitbox for {label} assigned yet.");
            return;
        }

        Debug.Log($"[BruteAttack] {label} missed.");
    }

    /// <summary>
    /// Shared OverlapBox query used by both RunHitCheck (swipe hits) and PollGrabWindow
    /// (the grab-then-kill path) -- one place that knows how to turn a reference
    /// BoxCollider's current world position into "is the player standing in it right
    /// now". Returns the player's PlayerHealth if found, otherwise null. Assumes box is
    /// non-null -- callers already skip null boxes.
    /// </summary>
    private PlayerHealth FindPlayerOverlap(BoxCollider box, out Vector3 hitPoint)
    {
        hitPoint = default;

        Vector3 worldCenter = box.transform.TransformPoint(box.center);
        Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, AbsScale(box.transform.lossyScale));

        // Collide (not Ignore) -- the player hurtbox is expected to be a trigger collider
        // (enlarged, non-blocking). hitableLayers already restricts this to the Player
        // layer, so this doesn't start picking up unrelated scene triggers.
        Collider[] hits = Physics.OverlapBox(
            worldCenter, halfExtents, box.transform.rotation,
            hitableLayers, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue; // skip own colliders

            // Require specifically the player, not "anything IDamageable" -- EnemyBase
            // also implements IDamageable (so player bullets can hit enemies through the
            // same interface), so a generic IDamageable lookup here can resolve to a
            // neighbouring enemy instead of the player when enemies are clustered.
            // See project-enemy-system.md 2026-08-10.
            PlayerHealth target = hit.GetComponentInParent<PlayerHealth>();
            if (target == null) continue;

            hitPoint = hit.ClosestPoint(worldCenter);
            return target;
        }

        return null;
    }

    /// <summary>
    /// Pushes the player directly away from the Brute on a landed swipe hit. Not called
    /// from the instaKill branch above -- Grab kills outright, so knockback on it is
    /// pointless (and PlayerMovement.ApplyKnockback already no-ops once _isDead is true
    /// anyway, as a second line of defence).
    /// </summary>
    private void ApplyKnockback(Transform playerTransform)
    {
        if (_playerMovement == null) return;

        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward; // degenerate: same position
        dir.Normalize();

        Vector3 force = dir * knockbackForce + Vector3.up * knockbackUpwardKick;
        _playerMovement.ApplyKnockback(force);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Component-wise absolute value -- avoids negative half-extents from negative scale.</summary>
    private static Vector3 AbsScale(Vector3 v) =>
        new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // ─── Scene gizmos ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawHitboxGizmo(leftFistBox,  new Color(0.1f, 0.6f, 1f, 0.5f), "L");
        DrawHitboxGizmo(rightFistBox, new Color(1f, 0.4f, 0.1f, 0.5f), "R");
        DrawHitboxGizmo(grabHitbox,   new Color(0.9f, 0f, 0f, 0.6f),  "GRAB");
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
