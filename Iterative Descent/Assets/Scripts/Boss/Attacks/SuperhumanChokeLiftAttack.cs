using UnityEngine;

/// <summary>
/// Deimos's Superhuman Choke Lift -- Move 5, and the first Deimos attack that is a genuine
/// grab rather than an instant swing. Ming Kai asked for this to work like Phobos's grab
/// ("Thrusting Attack", see BruteAttack.cs's "Grab redesign" doc comment) -- catch the player,
/// freeze their movement, then resolve (deal damage) at the end of the animation, not the
/// instant of contact -- but using Deimos's existing OverlapSphere + forward-cone hit check
/// (the same pattern MMAKickAttack/DropkickAttack/FlyingKneeAttack already use) instead of
/// Phobos's BoxCollider/OverlapBox fist-hitbox setup. So this class borrows Phobos's SEQUENCE,
/// not its hit-detection mechanics:
///
///   1. OnHitboxOpen() (Animation Event) opens a grab WINDOW instead of dealing damage --
///      same idea as BruteAttack.OpenGrabHitboxWindow(), just renamed to fit this class's
///      already-existing override of the same method name.
///   2. While the window is open, Update() polls Physics.OverlapSphere(transform.position,
///      hitRange, playerLayer) + a forward-cone angle check every frame -- exactly
///      MMAKickAttack.OnHitboxOpen()'s hit-check body, just run repeatedly instead of once.
///      The first player found is caught: PlayerMovement.SetGrabbed(true, transform) freezes
///      their WASD input (mirrors BruteAttack.GrabPlayer()) and the window closes immediately
///      (only one target can ever be caught per swing).
///   3. If nothing overlaps before maxWindowOpenDuration elapses (a fallback in case
///      OnHitboxClose is never wired, same purpose as BruteAttack.grabHitboxOpenDuration),
///      the window auto-closes -- a clean miss.
///   4. OnAttackAnimEnd() (last frame of the clip) is where the catch actually resolves --
///      ResolveGrab() deals damage via IDamageable.TakeDamage(), releases the movement lock,
///      and THROWS the player free via DeimosAttackBase.ApplyKnockbackTo() (the same shared
///      knockback helper MMAKick/Dropkick/Flying Knee already use) -- all three happen
///      together, mirroring BruteAttack.ResolveGrabOutcome() firing at "the end of the
///      animation," not the moment of contact. This lines up naturally with
///      DeimosAttackBase's own contract for OnAttackAnimEnd() ("primary signal the attack is
///      done"), so no separate coroutine timing is needed here the way
///      BruteAttack.GrabSequence() needs one -- DeimosAttackBase's attackTimeoutDuration
///      safety net already covers "clip never finishes." Unlike Phobos's grab (which only
///      freezes+damages, no knockback -- see BruteAttack's own header comment), Deimos's lift
///      ends with an actual throw, so this is the one Deimos attack that both freezes movement
///      AND applies ApplyKnockbackTo(), just at different moments (freeze on catch, throw on
///      release) instead of the instant-together timing every swipe-style attack uses.
///
/// Audio: hitboxOpenClip is deliberately NOT played from OnHitboxOpen() -- the window opening
/// is not yet "dangerous," the player hasn't been caught. Instead it plays from ResolveGrab(),
/// the moment damage actually lands, matching DeimosAttackBase's own doc comment precedent for
/// SprintChargeAttack (ARES): "its OnHitboxOpen() is deliberately suppressed for damage
/// reasons... plays it from its charge coroutine instead." windupClip still plays normally and
/// automatically from Execute() (DeimosAttackBase), same as every other attack.
///
/// Safety valves against a permanently frozen player (mirrors BruteAttack's OnDisable ->
/// ReleaseGrabIfActive(), which exists for exactly this reason):
///   - CancelAttack() override releases the grab before calling base.CancelAttack() -- per
///     DeimosAttackBase's own doc comment, any attack that "takes manual control of ... other
///     lingering visual/physical state MUST override this and restore it."
///   - OnDisable() releases the grab too, in case this component (or the whole Deimos
///     GameObject) is disabled/destroyed mid-grab.
///   - Update() also checks for the one path CancelAttack()/OnDisable() can't catch: the base
///     class's own AttackTimeoutRoutine calls its private EndAttack() directly if
///     OnAttackAnimEnd's Animation Event is missing, which would clear IsActive without ever
///     running ResolveGrab(). If a player is still held once IsActive has gone false on its
///     own, this force-releases them (no damage -- same "released, not killed" outcome as
///     BruteAttack.ReleaseGrabIfActive()).
///
/// Animator wiring (patched into Chimera.controller alongside this script, 2026-09-11):
///   Any State -> Superhuman Choke Lift   [trigger: Lift, no exit time, cannot transition to self]
///   Superhuman Choke Lift -> Idle        [exit time 0.9, no condition]
/// Same "Any State" reasoning as every other Deimos attack -- DeimosAttackRegistry only ever
/// fires the trigger while CurrentState == Combat AND IsLocomotionReady.
///
/// Animation Event setup still needed on the Superhuman Choke Lift clip itself (Ming Kai,
/// in-Editor):
///   - "OnHitboxOpen"    -- frame the reach begins (opens the grab window)
///   - "OnHitboxClose"   -- frame the reach ends (closes the window if nothing was caught) --
///                          optional; maxWindowOpenDuration is the fallback if this is never
///                          wired, same relationship as every other *Delay/*Duration pair in
///                          this codebase's attack scripts.
///   - "OnAttackAnimEnd" -- last frame of the clip (resolves the grab -- damage + release --
///                          and signals attack complete)
/// Until OnHitboxOpen/OnAttackAnimEnd exist, the lift plays visually but never opens a grab
/// window at all, and ends via attackTimeoutDuration instead (logs a warning -- see
/// DeimosAttackBase).
///
/// Unity Setup:
///   - Attach to the Deimos root GameObject (same as Animator/DeimosStateMachine/the other
///     three attacks).
///   - Assign playerLayer in the Inspector.
///   - maxRange (inherited, DeimosAttackBase) should be >= hitRange with a little slack.
/// </summary>
public class SuperhumanChokeLiftAttack : DeimosAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Choke Lift Settings")]
    [Tooltip("Damage dealt to the player when the grab resolves (end of animation) -- not at " +
             "the moment of contact. Mirrors BruteAttack.grabDamage -- a flat, tunable value, " +
             "not an instant kill.")]
    public float damage = 35f;

    [Tooltip("Radius of the hit check sphere around Deimos, polled every frame the grab " +
             "window is open. Same OverlapSphere pattern as MMAKick/Dropkick/Flying Knee -- " +
             "just run repeatedly instead of once.")]
    public float hitRange = 2.3f;

    [Tooltip("Half-angle of the hit cone in front of Deimos (degrees).")]
    public float hitHalfAngle = 45f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Header("Grab Window")]
    [Tooltip("Fallback: seconds the grab window stays open if OnHitboxClose is never fired " +
             "by an Animation Event. Mirrors BruteAttack.grabHitboxOpenDuration's role. The " +
             "window also closes immediately the instant a player is caught, same as Phobos.")]
    public float maxWindowOpenDuration = 1f;

    [Header("Grab Feedback")]
    [Tooltip("Seconds the camera-shake pulse (PlayerMovement.Shake) lasts, fired the instant " +
             "the grab connects. Mirrors BruteAttack.grabShakeDuration.")]
    public float grabShakeDuration = 0.35f;

    [Tooltip("Peak magnitude (metres) of that camera-shake pulse. Mirrors " +
             "BruteAttack.grabShakeMagnitude.")]
    public float grabShakeMagnitude = 0.18f;

    [Header("Knockback (applied on release -- throws the player free at the end of the " +
            "animation, at the same moment damage lands)")]
    [Tooltip("Horizontal speed (m/s) given to the player, pushed straight away from Deimos, " +
             "the instant the grab resolves. Strongest of Deimos's four attacks by default -- " +
             "getting hurled out of a choke lift should read as the hardest throw of the set. " +
             "Hand-off to PlayerMovement.ApplyKnockback -- that component owns the actual decay " +
             "(see its knockbackRecoverySpeed field). Uses the same DeimosAttackBase." +
             "ApplyKnockbackTo() helper as MMAKick/Dropkick/Flying Knee -- the only difference " +
             "is where it's called from (ResolveGrab(), not OnHitboxOpen()).")]
    public float knockbackForce = 18f;

    [Tooltip("Upward component added on top of the horizontal push -- higher than any other " +
             "Deimos attack's, for a 'thrown clear' feel rather than a stagger. 0 = perfectly " +
             "horizontal.")]
    public float knockbackUpwardKick = 3f;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerLift = Animator.StringToHash("Lift");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator _animator;

    // ─── Grab state ─────────────────────────────────────────────────────────────
    private bool            _windowOpen;
    private float           _windowOpenTime;
    private IDamageable     _grabbedDamageable;
    private PlayerMovement  _grabbedMovement;
    private Collider        _grabbedCollider;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError("[SuperhumanChokeLiftAttack] No Animator found on this GameObject.");
    }

    protected override void Update()
    {
        base.Update();

        if (_windowOpen)
        {
            PollGrabWindow();

            // Fallback close in case OnHitboxClose was never wired as an Animation Event --
            // same relationship as every *Delay/*Duration pair elsewhere in this codebase.
            if (_windowOpen && Time.time - _windowOpenTime >= maxWindowOpenDuration)
                CloseWindow();
        }

        // Safety valve: DeimosAttackBase's own timeout coroutine can end this attack (clear
        // IsActive) without ever calling OnAttackAnimEnd(), if the clip is missing that
        // Animation Event. If that happens while we're still holding a player, release them
        // now rather than leaving them frozen forever -- no damage, same as an early release.
        if (_grabbedMovement != null && !IsActive)
            ForceReleaseGrab();
    }

    void OnDisable()
    {
        // Mirrors BruteAttack.OnDisable() -> ReleaseGrabIfActive(): if this component (or the
        // whole Deimos GameObject) is disabled/destroyed mid-grab, don't leave the player
        // permanently movement-locked.
        ForceReleaseGrab();
    }

    // ─── DeimosAttackBase Implementation ──────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerLift);
        Debug.Log("[SuperhumanChokeLiftAttack] Superhuman Choke Lift triggered.");
    }

    /// <summary>Takes manual control of the player's movement state while a grab is held, so
    /// per DeimosAttackBase's own contract this must override CancelAttack() and restore it --
    /// same reasoning as ARES's SprintChargeAttack/GroundSlamAttack/PounceAttack/
    /// CoreOverloadAttack overriding it for their own lingering state.</summary>
    public override void CancelAttack()
    {
        ForceReleaseGrab();
        _windowOpen = false;
        base.CancelAttack();
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    /// <summary>Opens the grab window -- deliberately does NOT call PlaySound(hitboxOpenClip)
    /// or deal damage here, unlike MMAKick/Dropkick/Flying Knee's OnHitboxOpen(). The attack
    /// only becomes "dangerous" once a player is actually caught and the grab resolves -- see
    /// ResolveGrab().</summary>
    public override void OnHitboxOpen()
    {
        if (!IsActive) return;
        if (_windowOpen) return; // idempotent, same as BruteAttack.OpenGrabHitboxWindow()

        _windowOpen     = true;
        _windowOpenTime = Time.time;
        Debug.Log("[SuperhumanChokeLiftAttack] Grab window OPEN.");
    }

    /// <summary>Closes the grab window if nothing was caught. Optional -- maxWindowOpenDuration
    /// is the fallback if this Animation Event is never wired.</summary>
    public override void OnHitboxClose()
    {
        CloseWindow();
    }

    /// <summary>Primary signal the attack is done (DeimosAttackBase's contract) -- this is also
    /// "the end of the animation" that BruteAttack.GrabSequence() waits for via
    /// grabRecoveryDuration before calling ResolveGrabOutcome(). Resolving here means no
    /// separate coroutine timing is needed for that half of Phobos's design.</summary>
    public override void OnAttackAnimEnd()
    {
        ResolveGrab();
        base.OnAttackAnimEnd(); // calls EndAttack()
    }

    // ─── Grab Window Polling ──────────────────────────────────────────────────────

    /// <summary>Same OverlapSphere + forward-cone check MMAKickAttack/DropkickAttack/
    /// FlyingKneeAttack run once from OnHitboxOpen() -- here it just runs every frame the
    /// window is open, stopping the moment something is caught (CatchPlayer sets
    /// _windowOpen = false), so only one target is ever caught per swing.</summary>
    void PollGrabWindow()
    {
        var hits = Physics.OverlapSphere(transform.position, hitRange, playerLayer);
        foreach (var hit in hits)
        {
            var dir = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) > hitHalfAngle) continue;

            var damageable = hit.GetComponent<IDamageable>();
            if (damageable == null) continue;

            CatchPlayer(hit, damageable);
            return;
        }
    }

    void CloseWindow()
    {
        if (!_windowOpen) return; // idempotent -- also true once CatchPlayer already closed it
        _windowOpen = false;
        Debug.Log("[SuperhumanChokeLiftAttack] Grab window CLOSE.");
    }

    /// <summary>The grab window found the player. Locks their movement immediately via
    /// PlayerMovement.SetGrabbed(true, transform) -- no damage yet, that's deferred to
    /// ResolveGrab() at the end of the animation. Mirrors BruteAttack.GrabPlayer().</summary>
    void CatchPlayer(Collider hit, IDamageable damageable)
    {
        _grabbedDamageable = damageable;
        _grabbedCollider   = hit;
        _grabbedMovement   = hit.GetComponent<PlayerMovement>();
        _windowOpen        = false;

        if (_grabbedMovement != null)
        {
            // transform = this Deimos -- forces the player's camera to stay locked onto
            // whatever is holding them, same as PlayerMovement's own doc comment describes
            // for BruteAttack's Thrusting Attack. Cleared the moment SetGrabbed(false) is
            // called (ResolveGrab() or ForceReleaseGrab()).
            _grabbedMovement.SetGrabbed(true, transform);
            _grabbedMovement.Shake(grabShakeDuration, grabShakeMagnitude);
        }
        else
        {
            Debug.LogWarning("[SuperhumanChokeLiftAttack] Grab connected but no PlayerMovement " +
                              "was found to freeze -- player will not be movement-locked, but " +
                              "damage will still resolve at the end of the animation.");
        }

        Debug.Log($"[SuperhumanChokeLiftAttack] GRAB CONNECTED on '{hit.gameObject.name}' -- " +
                  "movement locked, damage resolves at the end of the animation.");
    }

    /// <summary>Called at the true end of the attack (OnAttackAnimEnd()). If PollGrabWindow ever
    /// caught a player this swing, this is where damage lands, the movement lock is released,
    /// AND the player is thrown free via ApplyKnockbackTo() -- mirrors BruteAttack.
    /// ResolveGrabOutcome() firing "at animation end," not the moment of contact, but unlike
    /// Phobos's grab, Deimos's lift ends with an actual throw rather than just letting go. All
    /// three (release, damage, knockback) happen together here. No-op on a clean miss.</summary>
    void ResolveGrab()
    {
        if (_grabbedDamageable == null) return;

        var damageable = _grabbedDamageable;
        var grabbedCol = _grabbedCollider;
        var movement   = _grabbedMovement;
        _grabbedDamageable = null; // clear first so a re-entrant call is a no-op
        _grabbedCollider   = null;
        _grabbedMovement   = null;

        movement?.SetGrabbed(false);

        PlaySound(hitboxOpenClip); // the attack becomes "dangerous" here, not at window-open

        var hitPoint = grabbedCol != null ? grabbedCol.ClosestPoint(transform.position)
                                           : transform.position;
        damageable.TakeDamage(damage, hitPoint);

        // Throw the player free -- same shared helper MMAKick/Dropkick/Flying Knee use, just
        // called from here (resolve) instead of OnHitboxOpen(), since the grab defers
        // everything dangerous to the end of the animation.
        if (grabbedCol != null)
            ApplyKnockbackTo(grabbedCol, knockbackForce, knockbackUpwardKick);

        Debug.Log($"[SuperhumanChokeLiftAttack] Grab resolved -- dealt {damage:F0} damage and " +
                  "threw the player free at animation end.");
    }

    /// <summary>Releases a grab in progress WITHOUT dealing damage or knockback -- called from
    /// CancelAttack(), OnDisable(), and Update()'s timeout safety check. Mirrors
    /// BruteAttack.ReleaseGrabIfActive(): prevents the player being left permanently
    /// movement-locked if this attack ends any way other than its normal resolve. "Released, not
    /// thrown" -- an early release is not a hit, so no ApplyKnockbackTo() call here either.</summary>
    void ForceReleaseGrab()
    {
        if (_grabbedMovement == null) return;

        _grabbedMovement.SetGrabbed(false);
        Debug.Log("[SuperhumanChokeLiftAttack] Grab released early -- movement restored, no damage or knockback applied.");

        _grabbedDamageable = null;
        _grabbedCollider   = null;
        _grabbedMovement   = null;
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.7f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}
