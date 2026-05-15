using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    private Animator _animator;
    private bool _isAiming;

    // Animator parameter hashes
    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int FireHash     = Animator.StringToHash("Fire");

    // Read by PlayerPropScript
    public bool IsAiming => _isAiming;

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        HandleAiming();
        HandleFiring();
    }

    // ─── Aiming ─────────────────────────────────────────────────────────────

    void HandleAiming()
    {
        _isAiming = Mouse.current.rightButton.isPressed;
        _animator.SetBool(IsAimingHash, _isAiming);
    }

    // ─── Firing ─────────────────────────────────────────────────────────────

    void HandleFiring()
    {
        if (!_isAiming) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _animator.SetTrigger(FireHash);

            // TODO: Add actual firing logic here (raycasts, damage, effects)
        }
    }
}