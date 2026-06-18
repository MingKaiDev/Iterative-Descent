using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Movement")]
    public float walkSpeed          = 3f;
    public float sprintSpeed        = 6f;
    public float aimWalkSpeed       = 1.8f;   // RE-style slow strafe while aimed
    public float gravity            = -9.81f;
    public float rotationSmoothTime = 0.1f;

    [Header("Third-Person Camera — Default")]
    public Transform cameraTransform;
    public float cameraDistance   = 1.5f;
    public float sideDistance     = 0f;
    public float cameraHeight     = 1.4f;
    public float cameraSensitivity = 3f;
    public float cameraMinY       = -20f;
    public float cameraMaxY       = 60f;

    [Header("Third-Person Camera — Aim (Over-the-Shoulder)")]
    [Tooltip("How far right the camera shifts when aiming. Positive = right shoulder.")]
    public float aimSideDistance   = 0.85f;
    [Tooltip("How close the camera gets when aiming.")]
    public float aimCameraDistance = 1.0f;
    [Tooltip("Slight height raise when aiming for a cleaner OTS look.")]
    public float aimCameraHeight   = 1.55f;
    [Tooltip("How fast the camera lerps between default and aim positions.")]
    public float cameraTransitionSpeed = 10f;
    [Tooltip("How far forward the camera look-target shifts when aiming. " +
             "Creates the OTS 'looking past the shoulder' effect. Try 2-3.")]
    public float aimLookAhead      = 2.5f;

    // ─── Public Read-Only State ─────────────────────────────────────────────────

    /// <summary>True while the player is sprinting. Read by PlayerCombat to block aim and reload.</summary>
    public bool IsRunning { get; private set; }

    // ─── Private ────────────────────────────────────────────────────────────────

    private CharacterController _controller;
    private Animator            _animator;
    private PlayerCombat        _combat;
    private ShotgunController   _shotgun;

    private Vector3 _velocity;
    private float   _rotationVelocity;
    private float   _camYaw;
    private float   _camPitch = 15f;
    private bool    _isDead;

    // Smoothed camera params (lerped each frame)
    private float _currentSide;
    private float _currentDist;
    private float _currentHeight;
    private float _currentLookAhead;  // smoothed look-ahead for OTS transition

    // Animator parameter hashes
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsAimingHash  = Animator.StringToHash("IsAiming");

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _animator   = GetComponent<Animator>();
        _combat     = GetComponent<PlayerCombat>();
        _shotgun    = GetComponent<ShotgunController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        _camYaw           = transform.eulerAngles.y;
        _currentSide      = sideDistance;
        _currentDist      = cameraDistance;
        _currentHeight    = cameraHeight;
        _currentLookAhead = 0f;

        PlayerHealth.OnPlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
    }

    void Update()
    {
        if (_isDead) return;

        HandleCameraRotation();
        HandleMovement();
        ApplyGravity();
    }

    // ─── Camera ─────────────────────────────────────────────────────────────────

    // Returns true if any equipped weapon is currently in aim stance.
    bool IsAnyWeaponAiming() =>
        (_combat  != null && _combat.enabled  && _combat.IsAiming) ||
        (_shotgun != null && _shotgun.enabled && _shotgun.IsAiming);

    // Cancels aim on whichever weapon is currently active.
    void CancelActiveAim()
    {
        _combat?.CancelAim();
        _shotgun?.CancelAim();
    }

    void HandleCameraRotation()
    {
        bool isAiming = IsAnyWeaponAiming();

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        _camYaw   += mouseDelta.x * cameraSensitivity * Time.deltaTime * 10f;
        _camPitch -= mouseDelta.y * cameraSensitivity * Time.deltaTime * 10f;
        _camPitch  = Mathf.Clamp(_camPitch, cameraMinY, cameraMaxY);

        // Lerp toward aim or default camera offsets
        float targetSide   = isAiming ? aimSideDistance   : sideDistance;
        float targetDist   = isAiming ? aimCameraDistance  : cameraDistance;
        float targetHeight = isAiming ? aimCameraHeight    : cameraHeight;

        float t = cameraTransitionSpeed * Time.deltaTime;
        _currentSide   = Mathf.Lerp(_currentSide,   targetSide,   t);
        _currentDist   = Mathf.Lerp(_currentDist,   targetDist,   t);
        _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, t);

        Quaternion camRotation = Quaternion.Euler(_camPitch, _camYaw, 0f);
        Vector3    focusPoint  = transform.position + Vector3.up * _currentHeight;

        // Smooth the look-ahead so it eases in/out when toggling aim.
        _currentLookAhead = Mathf.Lerp(_currentLookAhead,
                                        isAiming ? aimLookAhead : 0f,
                                        cameraTransitionSpeed * Time.deltaTime);

        cameraTransform.position = focusPoint + camRotation * new Vector3(_currentSide, 0f, -_currentDist);
        // When aiming, look past the player toward where they're facing for a proper OTS frame.
        cameraTransform.LookAt(focusPoint + transform.forward * _currentLookAhead);
    }

    // ─── Movement ───────────────────────────────────────────────────────────────

    void HandleMovement()
    {
        bool isAiming = IsAnyWeaponAiming();

        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;

        bool isSprinting = !isAiming && Keyboard.current.leftShiftKey.isPressed;
        bool isMoving    = input.magnitude >= 0.1f;
        bool isRunning   = isMoving && isSprinting;

        // Expose running state so PlayerCombat can gate aim and reload.
        IsRunning = isRunning;

        // If the player starts sprinting while aimed, cancel the aim stance.
        if (IsRunning && IsAnyWeaponAiming())
            CancelActiveAim();

        float currentSpeed = isAiming  ? aimWalkSpeed  :
                             isRunning ? sprintSpeed   :
                                         walkSpeed;

        // Keep animator states consistent regardless of mode
        _animator.SetBool(IsWalkingHash, isMoving);   // true for both walk & run; Running layered on top
        _animator.SetBool(IsRunningHash, isRunning);
        // IsAiming is driven by PlayerCombat directly, but keep movement-side in sync
        _animator.SetBool(IsAimingHash, isAiming);

        if (isAiming)
        {
            // ── Aim mode: character faces camera yaw, strafes relative to it ──
            // Snap directly to camera yaw -- RE4-style instant tracking so the
            // player model always faces exactly where the crosshair is pointing.
            transform.rotation = Quaternion.Euler(0f, _camYaw, 0f);

            if (isMoving)
            {
                Vector3 inputDir = new Vector3(input.x, 0f, input.y).normalized;
                // Move relative to camera facing (W = forward, A = strafe left, etc.)
                Vector3 moveDir = Quaternion.Euler(0f, _camYaw, 0f) * inputDir;
                _controller.Move(moveDir * currentSpeed * Time.deltaTime);
            }
        }
        else
        {
            // ── Normal mode: character rotates to face movement direction ──
            if (!isMoving) return;

            Vector3 inputDir    = new Vector3(input.x, 0f, input.y).normalized;
            float   targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + _camYaw;

            float smoothAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, targetAngle,
                ref _rotationVelocity, rotationSmoothTime);

            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            _controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);
        }
    }

    // ─── Gravity ────────────────────────────────────────────────────────────────

    void ApplyGravity()
    {
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    // ─── Death ──────────────────────────────────────────────────────────────────

    void HandlePlayerDied()
    {
        _isDead = true;
        IsRunning = false;

        // Unlock cursor so the player can click the restart button on the death overlay.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // TODO: Set animator trigger "Die" once the death animation state is set up
        // in the Animator Controller. Example: _animator.SetTrigger("Die");
        _animator.SetTrigger("Die");
    }
}
