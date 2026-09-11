using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages weapon swapping via number keys (1 = Pistol, 2 = Shotgun, 3 = Rifle).
///
/// Setup:
///   Add this component to the Player GameObject alongside PlayerCombat,
///   ShotgunController, and RifleController.
///   ShotgunController and RifleController must start DISABLED in the Inspector.
///   ShotgunPickupProp calls UnlockShotgun() when the player picks up the shotgun.
///   RiflePickupProp calls UnlockRifle() when the player picks up the rifle.
///
/// Rules:
///   - Cannot swap while the current weapon is reloading.
///   - Cannot swap to a weapon before it has been picked up.
///   - Swapping cancels aim on the outgoing weapon.
/// </summary>
public class WeaponManager : MonoBehaviour
{
    // ─── Private ─────────────────────────────────────────────────────────────────

    private PlayerCombat      _pistol;
    private ShotgunController _shotgun;
    private RifleController   _rifle;

    private bool _shotgunUnlocked;
    private bool _rifleUnlocked;
    private int  _currentIndex;   // 0 = pistol, 1 = shotgun, 2 = rifle

    /// <summary>True once the player has picked up the shotgun.</summary>
    public bool IsShotgunUnlocked => _shotgunUnlocked;
    /// <summary>True once the player has picked up the rifle.</summary>
    public bool IsRifleUnlocked => _rifleUnlocked;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _pistol  = GetComponent<PlayerCombat>();
        _shotgun = GetComponent<ShotgunController>();
        _rifle   = GetComponent<RifleController>();

        if (_pistol  == null) Debug.LogError("[WeaponManager] PlayerCombat not found on Player.");
        if (_shotgun == null) Debug.LogError("[WeaponManager] ShotgunController not found on Player.");
        if (_rifle   == null) Debug.LogError("[WeaponManager] RifleController not found on Player.");
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
        if (Keyboard.current.digit3Key.wasPressedThisFrame) TrySwitchTo(2);
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

    /// <summary>
    /// Called by RiflePickupProp when the player picks up the rifle for the first time.
    /// Sets the starting ammo and immediately equips the rifle.
    /// </summary>
    public void UnlockRifle(int mag, int spare)
    {
        _rifleUnlocked = true;
        _rifle.InitAmmo(mag, spare);
        SetWeapon(2, broadcastAmmo: true);
        Debug.Log($"[WeaponManager] Rifle unlocked. Mag: {mag}, Spare: {spare}.");
    }

    /// <summary>
    /// Re-fires the currently-equipped weapon's ammo broadcast without changing anything --
    /// called by LevelTransitionManager right after a cross-scene level transition, since
    /// the Player (and every weapon component on it) survives the scene load via
    /// PersistentPlayer/DontDestroyOnLoad, so none of the normal OnEnable()-driven
    /// broadcasts refire on their own for the new scene's fresh HUD.
    /// </summary>
    public void BroadcastCurrentWeaponState()
    {
        switch (_currentIndex)
        {
            case 0: _pistol?.BroadcastCurrentState();  break;
            case 1: _shotgun?.BroadcastCurrentState(); break;
            case 2: _rifle?.BroadcastCurrentState();   break;
        }
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

        if (index == 2 && !_rifleUnlocked)
        {
            Debug.Log("[WeaponManager] Rifle not yet picked up.");
            return;
        }

        // Block mid-reload to avoid ammo desync.
        bool isReloading = (_currentIndex == 0 && _pistol  != null && _pistol.IsReloading)
                        || (_currentIndex == 1 && _shotgun != null && _shotgun.IsReloading)
                        || (_currentIndex == 2 && _rifle   != null && _rifle.IsReloading);
        if (isReloading) return;

        SetWeapon(index, broadcastAmmo: true);
    }

    void SetWeapon(int index, bool broadcastAmmo)
    {
        // Cancel aim on all weapons before switching.
        _pistol?.CancelAim();
        _shotgun?.CancelAim();
        _rifle?.CancelAim();

        _currentIndex = index;

        // Toggle components -- OnEnable/OnDisable on each weapon handles
        // model visibility and ammo HUD broadcast.
        bool pistolActive  = (index == 0);
        bool shotgunActive = (index == 1);
        bool rifleActive   = (index == 2);

        if (_pistol  != null) _pistol.enabled  = pistolActive;
        if (_shotgun != null) _shotgun.enabled = shotgunActive;
        if (_rifle   != null) _rifle.enabled   = rifleActive;

        // OnEnable of the newly active weapon broadcasts ammo automatically.
        // broadcastAmmo param is kept for clarity but not needed to be called
        // explicitly -- Unity fires OnEnable before the next Update.
    }
}
