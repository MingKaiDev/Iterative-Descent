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
    [Tooltip("Degrees per second the recoil offset returns to zero.")]
    public float recoilDecaySpeed  = 12f;

    // --- Public Read-Only State ---------------------------------------------

    /// <summary>True while the player is sprinting. Read by weapon scripts.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>True while moving but not sprinting (i.e. walking). Read by PlayerAudioController for pistol walk footsteps.</summary>
    public bool IsWalking { get; private set; }

    /// <summary>Current camera pitch in degrees. Read by UpperBodyAim to drive spine bone rotation.</summary>
    public float CameraPitch => _pitch;

    // --- Private ------------------------------------------------------------

    private CharacterController _controller;
    private Animator            _animator;

    private Vector3 _velocity;
    private float   _yaw;
    private float   _pitch;
    private bool    _isDead;

    private float _recoilOffset;

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

        _yaw   = transform.eulerAngles.y;
        _pitch = 0f;

        PlayerHealth.OnPlayerDied += HandlePlayerDied;

        OnPlayerSpawned?.Invoke();
    }

    void OnEnable()
    {
        PlayerCombat.OnFired      += HandlePistolFired;
        ShotgunController.OnFired += HandleShotgunFired;
    }

    void OnDisable()
    {
        PlayerCombat.OnFired      -= HandlePistolFired;
        ShotgunController.OnFired -= HandleShotgunFired;
    }

    void OnDestroy()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
    }

    void Update()
    {
        if (_isDead) return;

        HandleLook();
        HandleMovement();
        ApplyGravity();
    }

    // --- Look ---------------------------------------------------------------

    void HandleLook()
    {
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

    void HandlePistolFired()
    {
        _recoilOffset -= pistolRecoilKick;
    }

    void HandleShotgunFired()
    {
        _recoilOffset -= shotgunRecoilKick;
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

    // --- Death --------------------------------------------------------------

    void HandlePlayerDied()
    {
        _isDead   = true;
        IsRunning = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        _animator.SetTrigger("Die");
    }
}
