using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    // --- Events ---------------------------------------------------------------

    /// <summary>
    /// Fired once, at the end of Start(), when this Player instance becomes active
    /// in the scene. Use this instead of a trigger collider for "on spawn" logic
    /// (e.g. initial dialogue) - trigger colliders are unreliable when the player
    /// already overlaps them at scene load instead of walking into them.
    /// </summary>
    public static event Action OnPlayerSpawned;

    // --- Inspector ----------------------------------------------------------

    [Header("Movement")]
    public float walkSpeed   = 3f;
    public float sprintSpeed = 6f;
    public float gravity     = -9.81f;
    [Tooltip("Multiplier applied to walkSpeed when moving backward. Also prevents sprinting.")]
    [Range(0.1f, 1f)]
    public float backwardSpeedMultiplier = 0.7f;

    [Header("FPS Camera")]
    [Tooltip("Assign the Camera child GameObject parented to the Player at head height.")]
    public Transform cameraTransform;
    [Tooltip("Editor-only fallback. Overwritten in Start() by SettingsManager.MouseSensitivity (player's saved slider value), and live-updated by SettingsPanelUI while the settings panel is open.")]
    public float mouseSensitivity = SettingsManager.DefaultMouseSensitivity;
    public float pitchMin         = -80f;
    public float pitchMax         = 80f;

    [Header("Recoil")]
    [Tooltip("Degrees the camera kicks upward per pistol shot.")]
    public float pistolRecoilKick  = 2f;
    [Tooltip("Degrees the camera kicks upward per shotgun shot.")]
    public float shotgunRecoilKick = 6f;
    [Tooltip("Degrees the camera kicks upward per rifle shot. Defaults to match " +
             "shotgunRecoilKick per design request -- the rifle's 100 dmg round kicks as hard " +
             "as the shotgun despite firing one round instead of a pellet spread.")]
    public float rifleRecoilKick   = 6f;
    [Tooltip("Degrees per second the recoil offset returns to zero.")]
    public float recoilDecaySpeed  = 12f;

    [Header("Knockback")]
    [Tooltip("How quickly an applied knockback velocity decays back to zero (higher = " +
             "shorter, snappier stagger). Used as a Lerp factor in ApplyKnockbackMovement, " +
             "not a flat per-second subtraction, so it slows down as it approaches zero " +
             "rather than stopping abruptly.")]
    public float knockbackRecoverySpeed = 6f;

    [Header("Camera Shake")]
    [Tooltip("Continuous small camera jitter applied for as long as IsGrabbed is true, on " +
             "top of any one-off Shake() pulse -- keeps a grab feeling like an active " +
             "struggle instead of a freeze-frame. 0 = off.")]
    public float grabbedRumbleMagnitude = 0.015f;

    [Header("Grab Look")]
    [Tooltip("Degrees/second-equivalent Lerp speed used to whip the camera around to face " +
             "whatever grabbed the player (see SetGrabbed's grabber param) and hold it " +
             "there -- higher = snappier whip, lower = a slower, heavier drag. Mouse look " +
             "is completely ignored while grabbed, so this is the only thing driving the " +
             "camera during a grab.")]
    public float grabLookSnapSpeed = 10f;

    // --- Public Read-Only State ---------------------------------------------

    /// <summary>True while the player is sprinting. Read by weapon scripts.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>True while moving but not sprinting (i.e. walking). Read by PlayerAudioController for pistol walk footsteps.</summary>
    public bool IsWalking { get; private set; }

    /// <summary>Current camera pitch in degrees. Read by UpperBodyAim to drive spine bone rotation.</summary>
    public float CameraPitch => _pitch;

    /// <summary>
    /// True while an enemy's grab attack (e.g. BruteAttack's Thrusting Attack) is holding
    /// the player in place. WASD movement is ignored while this is true -- look input,
    /// gravity and knockback keep running so the player can still look around and stays
    /// grounded. Set via SetGrabbed(), called by the grabbing enemy.
    /// </summary>
    public bool IsGrabbed { get; private set; }

    // --- Private ------------------------------------------------------------

    private CharacterController _controller;
    private Animator            _animator;

    private Vector3 _velocity;
    private Vector3 _externalVelocity; // knockback -- decays via ApplyKnockbackMovement
    private float   _yaw;
    private float   _pitch;
    private bool    _isDead;

    private float _recoilOffset;

    // Camera shake -- see Shake()/ApplyCameraShake(). _cameraBaseLocalPosition is
    // captured once in Start() so shake can be a pure additive offset from the camera's
    // real rest position instead of drifting it frame over frame.
    private Vector3 _cameraBaseLocalPosition;
    private float   _shakeTimer;      // seconds remaining on the current one-off pulse
    private float   _shakeDuration;   // total duration of that pulse, for linear falloff
    private float   _shakeMagnitude;  // peak magnitude of that pulse

    // Grab look -- see SetGrabbed()/UpdateGrabLook(). Null whenever not grabbed.
    private Transform _grabbedBy;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    // --- Unity Lifecycle ----------------------------------------------------

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _animator   = GetComponent<Animator>();

        // Pull the player's saved sensitivity (or the default, if none saved yet).
        // Runs after any manual Inspector value, so it always wins at runtime --
        // SettingsPanelUI then live-updates this field while the panel is open.
        mouseSensitivity = SettingsManager.MouseSensitivity;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            _cameraBaseLocalPosition = cameraTransform.localPosition;

        _yaw   = transform.eulerAngles.y;
        _pitch = 0f;

        PlayerHealth.OnPlayerDied += HandlePlayerDied;

        OnPlayerSpawned?.Invoke();
    }

    void OnEnable()
    {
        PlayerCombat.OnFired      += HandlePistolFired;
        ShotgunController.OnFired += HandleShotgunFired;
        RifleController.OnFired   += HandleRifleFired;
    }

    void OnDisable()
    {
        PlayerCombat.OnFired      -= HandlePistolFired;
        ShotgunController.OnFired -= HandleShotgunFired;
        RifleController.OnFired   -= HandleRifleFired;
    }

    void OnDestroy()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
    }

    void Update()
    {
        if (_isDead) return;

        HandleLook();
        if (!IsGrabbed) // frozen by an enemy's grab attack -- see SetGrabbed
            HandleMovement();
        ApplyGravity();
        ApplyKnockbackMovement();
        ApplyCameraShake();
    }

    // --- Look ---------------------------------------------------------------

    void HandleLook()
    {
        // While grabbed, mouse input is completely ignored -- the enemy is forcing the
        // player's view onto itself (classic "you can't look away" horror beat), not just
        // adding to whatever they're already doing. See SetGrabbed()/UpdateGrabLook().
        if (IsGrabbed && _grabbedBy != null)
        {
            UpdateGrabLook();
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        _yaw   += mouseDelta.x * mouseSensitivity * Time.deltaTime * 10f;
        _pitch -= mouseDelta.y * mouseSensitivity * Time.deltaTime * 10f;
        _pitch  = Mathf.Clamp(_pitch, pitchMin, pitchMax);

        // Decay recoil offset back to zero each frame.
        _recoilOffset = Mathf.MoveTowards(_recoilOffset, 0f, recoilDecaySpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(_pitch + _recoilOffset, 0f, 0f);
    }

    /// <summary>
    /// Whips _yaw/_pitch toward whatever grabbed the player (_grabbedBy) and holds them
    /// there, instead of following the mouse. Uses the same Quaternion.Euler(pitch,0,0) /
    /// Euler(0,yaw,0) split as the normal path above so it stays consistent with
    /// UpperBodyAim and anything else reading CameraPitch. Runs every frame while
    /// grabbed, not just once, so it keeps tracking if the grabbing enemy moves or
    /// rotates during the hold.
    /// </summary>
    void UpdateGrabLook()
    {
        Vector3 eye = cameraTransform != null ? cameraTransform.position : transform.position;
        Vector3 toGrabber = _grabbedBy.position - eye;

        if (toGrabber.sqrMagnitude > 0.0001f)
        {
            Vector3 flatDir = toGrabber; flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                float targetYaw = Quaternion.LookRotation(flatDir.normalized).eulerAngles.y;
                _yaw = Mathf.LerpAngle(_yaw, targetYaw, grabLookSnapSpeed * Time.deltaTime);
            }

            float horizontalDist = flatDir.magnitude;
            float targetPitch = -Mathf.Atan2(toGrabber.y, Mathf.Max(horizontalDist, 0.0001f)) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(targetPitch, pitchMin, pitchMax);
            _pitch = Mathf.Lerp(_pitch, targetPitch, grabLookSnapSpeed * Time.deltaTime);
        }

        _recoilOffset = Mathf.MoveTowards(_recoilOffset, 0f, recoilDecaySpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(_pitch + _recoilOffset, 0f, 0f);
    }

    void HandlePistolFired()
    {
        _recoilOffset -= pistolRecoilKick;
    }

    void HandleShotgunFired()
    {
        _recoilOffset -= shotgunRecoilKick;
    }

    void HandleRifleFired()
    {
        _recoilOffset -= rifleRecoilKick;
    }

    // --- Movement -----------------------------------------------------------

    void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;

        bool isMoving   = input.magnitude >= 0.1f;
        bool isBackward = input.y < 0f;
        bool isRunning  = isMoving && !isBackward && Keyboard.current.leftShiftKey.isPressed;

        IsRunning = isRunning;
        IsWalking = isMoving && !isRunning;

        float speed = isRunning ? sprintSpeed
                    : isBackward ? walkSpeed * backwardSpeedMultiplier
                    : walkSpeed;

        _animator.SetBool(IsWalkingHash, isMoving);
        _animator.SetBool(IsRunningHash, isRunning);

        if (!isMoving) return;

        Vector3 moveDir = (transform.right * input.x + transform.forward * input.y).normalized;
        _controller.Move(moveDir * speed * Time.deltaTime);
    }

    // --- Gravity ------------------------------------------------------------

    void ApplyGravity()
    {
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    // --- Knockback ------------------------------------------------------------

    /// <summary>
    /// Applies an instantaneous external velocity on top of normal input movement --
    /// called by enemy attack scripts (e.g. BruteAttack) on a landed melee hit. Routed
    /// through here rather than moving transform.position directly, since this class
    /// owns the CharacterController and a direct transform move would just get undone
    /// by the next _controller.Move() collision resolution.
    /// Overwrites rather than adds, so a second hit landing before the first knockback
    /// has fully decayed does not stack into an ever-increasing launch.
    /// </summary>
    public void ApplyKnockback(Vector3 force)
    {
        if (_isDead) return;
        _externalVelocity = force;
    }

    // --- Grab -----------------------------------------------------------------

    /// <summary>
    /// Called by an enemy's grab attack (e.g. BruteAttack.GrabPlayer/ResolveGrabOutcome/
    /// ReleaseGrabIfActive) the instant its grab hitbox connects, and again with false
    /// once the grab resolves (instant kill) or is released early (e.g. the grabbing
    /// enemy is destroyed mid-animation). Gates movement (IsGrabbed, see Update()) and
    /// drives the forced grab-look (grabber, see UpdateGrabLook()) -- safe to call at any
    /// time, including after the player is already dead (Update() has already bailed out
    /// on _isDead by then anyway).
    /// </summary>
    /// <param name="grabbed">True to start/keep the grab, false to release it.</param>
    /// <param name="grabber">The Transform the camera should be forced to face while
    /// grabbed (typically the grabbing enemy's own transform). Ignored when grabbed is
    /// false -- releasing always clears it, whatever is passed.</param>
    public void SetGrabbed(bool grabbed, Transform grabber = null)
    {
        IsGrabbed  = grabbed;
        _grabbedBy = grabbed ? grabber : null;
    }

    /// <summary>
    /// Starts (or restarts) a one-off camera-position shake pulse, e.g. the impact moment
    /// of an enemy's grab connecting. Overwrites rather than stacks -- a second call
    /// before the first pulse finishes just restarts at the new duration/magnitude,
    /// same "overwrite not stack" convention as ApplyKnockback -- so repeated triggers
    /// stay predictable instead of compounding into something jittery.
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        _shakeDuration  = Mathf.Max(duration, 0.0001f);
        _shakeTimer     = duration;
        _shakeMagnitude = magnitude;
    }

    /// <summary>
    /// Applies the current one-off shake pulse (linear falloff over its duration) plus
    /// the continuous grabbedRumbleMagnitude jitter while IsGrabbed, as a pure additive
    /// offset from _cameraBaseLocalPosition -- never accumulates, so it can't drift the
    /// camera away from its real rest position over a long play session.
    /// </summary>
    void ApplyCameraShake()
    {
        if (cameraTransform == null) return;

        Vector3 offset = Vector3.zero;

        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;
            float falloff = Mathf.Clamp01(_shakeTimer / _shakeDuration);
            offset += UnityEngine.Random.insideUnitSphere * (_shakeMagnitude * falloff);
        }

        if (IsGrabbed && grabbedRumbleMagnitude > 0f)
            offset += UnityEngine.Random.insideUnitSphere * grabbedRumbleMagnitude;

        cameraTransform.localPosition = _cameraBaseLocalPosition + offset;
    }

    void ApplyKnockbackMovement()
    {
        if (_externalVelocity.sqrMagnitude < 0.0001f) return;

        _controller.Move(_externalVelocity * Time.deltaTime);
        _externalVelocity = Vector3.Lerp(_externalVelocity, Vector3.zero, knockbackRecoverySpeed * Time.deltaTime);
    }

    // --- Death --------------------------------------------------------------

    void HandlePlayerDied()
    {
        _isDead   = true;
        IsRunning = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        _animator.SetTrigger("Die");
    }

    // --- Checkpoint Respawn ---------------------------------------------------

    /// <summary>
    /// Moves the player to a checkpoint's saved position/facing without the normal
    /// CharacterController collision resolution fighting a direct transform.position
    /// set -- disables the controller for the move, then re-enables it. Called by
    /// CheckpointManager as part of an in-place respawn (no scene reload).
    /// </summary>
    public void TeleportTo(Vector3 position, float yaw)
    {
        if (_controller != null) _controller.enabled = false;

        transform.position = position;
        _yaw   = yaw;
        _pitch = 0f;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.identity;

        _velocity         = Vector3.zero;
        _externalVelocity = Vector3.zero;

        if (_controller != null) _controller.enabled = true;
    }

    /// <summary>
    /// Re-fires OnPlayerSpawned -- called by LevelTransitionManager right after teleporting
    /// the persisted player into a newly-loaded level. Start()'s own one-time firing of this
    /// event already happened back in the previous scene (the Player survives the scene load
    /// via PersistentPlayer/DontDestroyOnLoad, so Start() never runs again), so anything in the
    /// new scene that listens for "on spawn" (e.g. an intro dialogue) would otherwise never see
    /// it fire. Same motivation as Start()'s own doc comment: a trigger collider the player
    /// already overlaps at spawn is unreliable, so this event is the one to hook instead.
    /// </summary>
    public void NotifySpawned() => OnPlayerSpawned?.Invoke();

    /// <summary>
    /// Clears the dead flag and re-locks the cursor after a checkpoint respawn. Uses
    /// Animator.Rebind() to snap the Animator back to its default state -- the "Die"
    /// trigger drives a terminal death state with no exit transition, so a plain
    /// SetTrigger can't reverse it, but Rebind() resets the whole controller instantly.
    /// </summary>
    public void Revive()
    {
        _isDead    = false;
        IsGrabbed  = false;
        _grabbedBy = null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }
}
