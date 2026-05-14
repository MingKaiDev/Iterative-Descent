using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float sprintSpeed = 6f;
    public float gravity = -9.81f;
    public float rotationSmoothTime = 0.1f;

    [Header("Third Person Camera")]
    public Transform cameraTransform;
    public float cameraDistance = 1.5f;
    public float sideDistance = 0f;
    public float cameraHeight = 1.4f;
    public float cameraSensitivity = 3f;
    public float cameraMinY = -20f;
    public float cameraMaxY = 60f;

    private CharacterController _controller;
    private Animator _animator;
    private Vector3 _velocity;
    private float _rotationVelocity;
    private float _camYaw;
    private float _camPitch = 15f;

    // Animator parameter hashes
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        _camYaw = transform.eulerAngles.y;
    }

    void Update()
    {
        HandleCameraRotation();
        HandleMovement();
        ApplyGravity();
    }

    // ─── Camera ────────────────────────────────────────────────────────────────

    void HandleCameraRotation()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        _camYaw += mouseDelta.x * cameraSensitivity * Time.deltaTime * 10f;
        _camPitch -= mouseDelta.y * cameraSensitivity * Time.deltaTime * 10f;
        _camPitch = Mathf.Clamp(_camPitch, cameraMinY, cameraMaxY);

        Quaternion camRotation = Quaternion.Euler(_camPitch, _camYaw, 0f);
        Vector3 focusPoint = transform.position + Vector3.up * cameraHeight;

        cameraTransform.position = focusPoint + camRotation * new Vector3(sideDistance, 0f, -cameraDistance);
        cameraTransform.LookAt(focusPoint);
    }

    // ─── Movement ──────────────────────────────────────────────────────────────

    void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;

        bool isSprinting = Keyboard.current.leftShiftKey.isPressed;
        bool isMoving = input.magnitude >= 0.1f;
        bool isRunning = isMoving && isSprinting;
        float currentSpeed = isRunning ? sprintSpeed : walkSpeed;

        // Drive animator FSM: Idle → Walking → Running
        _animator.SetBool(IsWalkingHash, isMoving);
        _animator.SetBool(IsRunningHash, isRunning);

        if (!isMoving) return;

        Vector3 inputDir = new Vector3(input.x, 0f, input.y).normalized;
        float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + _camYaw;

        float smoothAngle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y, targetAngle,
            ref _rotationVelocity, rotationSmoothTime);

        transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        _controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);
    }

    // ─── Gravity ───────────────────────────────────────────────────────────────

    void ApplyGravity()
    {
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}