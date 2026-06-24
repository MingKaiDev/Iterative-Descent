using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages weapon swapping via number keys (1 = Pistol, 2 = Shotgun).
///
/// Setup:
///   Add this component to the Player GameObject alongside PlayerCombat and ShotgunController.
///   ShotgunController must start DISABLED in the Inspector.
///   ShotgunPickupProp calls UnlockShotgun() when the player picks up the shotgun.
///
/// Rules:
///   - Cannot swap while the current weapon is reloading.
///   - Cannot swap to the shotgun before it has been picked up.
///   - Swapping cancels aim on the outgoing weapon.
/// </summary>
public class WeaponManager : MonoBehaviour
{
    // ─── Private ─────────────────────────────────────────────────────────────────

    private PlayerCombat      _pistol;
    private ShotgunController _shotgun;

    private bool _shotgunUnlocked;
    private int  _currentIndex;   // 0 = pistol, 1 = shotgun

    /// <summary>True once the player has picked up the shotgun.</summary>
    public bool IsShotgunUnlocked => _shotgunUnlocked;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _pistol  = GetComponent<PlayerCombat>();
        _shotgun = GetComponent<ShotgunController>();

        if (_pistol  == null) Debug.LogError("[WeaponManager] PlayerCombat not found on Player.");
        if (_shotgun == null) Debug.LogError("[WeaponManager] ShotgunController not found on Player.");
    }

    void Start()
    {
        // Start with pistol active.
        SetWeapon(0, broadcastAmmo: false);
    }

    void Update()
    {
        if (PlayerInteractor.IsPaused) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) TrySwitchTo(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) TrySwitchTo(1);
    }

    // ─── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ShotgunPickupProp when the player picks up the shotgun for the first time.
    /// Sets the starting ammo and immediately equips the shotgun.
    /// </summary>
    public void UnlockShotgun(int mag, int spare)
    {
        _shotgunUnlocked = true;
        _shotgun.InitAmmo(mag, spare);
        SetWeapon(1, broadcastAmmo: true);
        Debug.Log($"[WeaponManager] Shotgun unlocked. Mag: {mag}, Spare: {spare}.");
    }

    // ─── Private ─────────────────────────────────────────────────────────────────

    void TrySwitchTo(int index)
    {
        if (index == _currentIndex) return;

        if (index == 1 && !_shotgunUnlocked)
        {
            Debug.Log("[WeaponManager] Shotgun not yet picked up.");
            return;
        }

        // Block mid-reload to avoid ammo desync.
        bool isReloading = (_currentIndex == 0 && _pistol  != null && _pistol.IsReloading)
                        || (_currentIndex == 1 && _shotgun != null && _shotgun.IsReloading);
        if (isReloading) return;

        SetWeapon(index, broadcastAmmo: true);
    }

    void SetWeapon(int index, bool broadcastAmmo)
    {
        // Cancel aim on both weapons before switching.
        _pistol?.CancelAim();
        _shotgun?.CancelAim();

        _currentIndex = index;

        // Toggle components -- OnEnable/OnDisable on each weapon handles
        // model visibility and ammo HUD broadcast.
        bool pistolActive  = (index == 0);
        bool shotgunActive = (index == 1);

        if (_pistol  != null) _pistol.enabled  = pistolActive;
        if (_shotgun != null) _shotgun.enabled = shotgunActive;

        // OnEnable of the newly active weapon broadcasts ammo automatically.
        // broadcastAmmo param is kept for clarity but not needed to be called
        // explicitly -- Unity fires OnEnable before the next Update.
    }
}
