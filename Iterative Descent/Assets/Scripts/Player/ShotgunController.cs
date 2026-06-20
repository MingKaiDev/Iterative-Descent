using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shotgun weapon controller. Mirrors PlayerCombat (pistol) in structure.
///
/// Key differences vs pistol:
///   - Projectile-based: fires N ShotgunPellet prefabs in a spread cone.
///   - NOT active by default -- component starts disabled on the Player.
///   - Enabled by ShotgunPickupProp.Interact() via EquipWithAmmo().
///   - Manages its own weapon model (shotgunModel shown, pistolModel hidden on enable).
///
/// Animator reuse:
///   Uses the same IsAiming / Fire / Reload animator params as the pistol.
///   The character will play pistol-rig animations with the shotgun mesh visible.
///   Add dedicated shotgun animator states when proper animations are available.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ShotgunController : MonoBehaviour
{
    // ─── Events ──────────────────────────────────────────────────────────────────
    // Mirror of PlayerCombat events -- CombatHUD subscribes to both sets.

    public static event Action<int, int> OnAmmoChanged;   // (shellsInMag, spareShells)
    public static event Action           OnFired;
    public static event Action           OnDryFire;
    public static event Action           OnReloadStart;    // fires once per shell inserted
    public static event Action           OnReloadComplete; // fires when reload ends (ran out of spare)
    public static event Action           OnMagFull;        // fires when magazine reaches max capacity

    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Shotgun - Ammo")]
    [Tooltip("Maximum shells the magazine holds.")]
    public int magazineSize      = 6;

    [Header("Shotgun - Firing")]
    [Tooltip("Number of pellets fired per shot.")]
    public int   pelletCount     = 6;
    [Tooltip("Half-angle of the spread cone in degrees. Larger = wider spread.")]
    public float spreadAngle     = 10f;
    [Tooltip("Speed of each pellet in m/s.")]
    public float pelletSpeed     = 30f;
    [Tooltip("Damage dealt by each pellet (6 pellets x 15 = 90 max per shot).")]
    public float pelletDamage    = 15f;
    [Tooltip("Minimum seconds between shots (pump-action feel).")]
    public float fireInterval    = 0.75f;

    [Header("Shotgun - Reload")]
    [Tooltip("Seconds between each shell insert. Reload loops one shell at a time.")]
    public float reloadTime           = 0.65f;
    [Tooltip("Seconds after firing before a reload is allowed. Match to fire animation clip.")]
    public float fireAnimationDuration = 0.7f;

    [Header("Prefabs")]
    [Tooltip("Assign the ShotgunPellet prefab (SphereCollider + Rigidbody + ShotgunPellet).")]
    public GameObject pelletPrefab;
    [Tooltip("Optional: where pellets spawn from. Leave unassigned to use camera position.")]
    public Transform  muzzlePoint;

    [Header("Effects (optional)")]
    public ParticleSystem muzzleFlash;
    public GameObject     bulletImpactPrefab;

    [Header("Weapon Models")]
    [Tooltip("Shotgun mesh child on the player. Shown when this weapon is active.")]
    public GameObject shotgunModel;
    [Tooltip("Pistol mesh child on the player. Hidden when shotgun is active.")]
    public GameObject pistolModel;

    // ─── Public Read-Only State ──────────────────────────────────────────────────

    public bool IsAiming    => _isAiming;
    public bool IsReloading => _isReloading;
    public int  CurrentMag  => _currentMag;
    public int  SpareShells => _spareShells;

    // ─── Private ─────────────────────────────────────────────────────────────────

    private Animator       _animator;
    private PlayerMovement _movement;
    private bool      _isAiming;
    private bool      _isReloading;
    private bool      _isDead;
    private int       _currentMag;
    private int       _spareShells;
    private float     _nextFireTime;
    private float     _lastFireTime = -999f;
    private Coroutine _reloadCoroutine;

    // Reuse same animator parameter hashes as pistol.
    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int FireHash     = Animator.StringToHash("Fire");
    private static readonly int ReloadHash   = Animator.StringToHash("Reload");

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _movement = GetComponent<PlayerMovement>();

        PlayerHealth.OnPlayerDied += HandlePlayerDied;

        // Hide shotgun model on startup -- not equipped yet.
        if (shotgunModel != null) shotgunModel.SetActive(false);
    }

    void OnDestroy()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
    }

    void OnEnable()
    {
        // Show shotgun, hide pistol.
        if (shotgunModel != null) shotgunModel.SetActive(true);
        if (pistolModel  != null) pistolModel.SetActive(false);

        BroadcastAmmo();
    }

    void OnDisable()
    {
        // Restore pistol visibility and clear aim state.
        CancelAim();
        if (shotgunModel != null) shotgunModel.SetActive(false);
        if (pistolModel  != null) pistolModel.SetActive(true);
    }

    void Update()
    {
        if (_isReloading || _isDead) return;

        if (PlayerInteractor.IsPaused)
        {
            CancelAim();
            return;
        }

        HandleAimToggle();
        HandleFiring();
        HandleReload();
    }

    // ─── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets ammo without enabling the component.
    /// Used by WeaponManager.UnlockShotgun() -- WeaponManager controls the enable state.
    /// </summary>
    public void InitAmmo(int mag, int spare)
    {
        _currentMag  = Mathf.Clamp(mag, 0, magazineSize);
        _spareShells = spare;
    }

    /// <summary>
    /// Sets ammo AND enables the component. Use only when WeaponManager is absent
    /// (e.g., legacy scene setup without weapon swapping).
    /// </summary>
    public void EquipWithAmmo(int mag, int spare)
    {
        _currentMag  = Mathf.Clamp(mag, 0, magazineSize);
        _spareShells = spare;
        enabled      = true;   // triggers OnEnable
    }

    /// <summary>
    /// Force aim off. Called by PlayerMovement when sprinting starts mid-aim.
    /// </summary>
    public void CancelAim()
    {
        if (!_isAiming) return;
        _isAiming = false;
        if (_animator != null)
            _animator.SetBool(IsAimingHash, false);
    }

    /// <summary>
    /// Add shells to the spare pool. For future ShotgunAmmoPickup use.
    /// </summary>
    public void AddAmmo(int amount)
    {
        _spareShells += amount;
        BroadcastAmmo();
    }

    // ─── Aim ─────────────────────────────────────────────────────────────────────

    void HandleAimToggle()
    {
        if (_movement != null && _movement.IsRunning) return;
        if (!Mouse.current.rightButton.wasPressedThisFrame) return;

        _isAiming = !_isAiming;
        _animator.SetBool(IsAimingHash, _isAiming);
    }

    // ─── Fire ────────────────────────────────────────────────────────────────────

    void HandleFiring()
    {
        if (!_isAiming)                                    return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.time < _nextFireTime)                     return;

        // Pump shotguns allow interrupting a reload to fire once a shell is loaded.
        if (_isReloading)
        {
            if (_currentMag <= 0) return;   // nothing loaded yet, keep reloading
            if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
            _isReloading     = false;
            _reloadCoroutine = null;
            // fall through to fire
        }

        _nextFireTime = Time.time + fireInterval;

        if (_currentMag <= 0)
        {
            OnDryFire?.Invoke();
            return;
        }

        _currentMag--;
        _lastFireTime = Time.time;
        _animator.SetTrigger(FireHash);
        if (muzzleFlash != null) muzzleFlash.Play();
        OnFired?.Invoke();
        BroadcastAmmo();

        FirePellets();
    }

    void FirePellets()
    {
        if (pelletPrefab == null)
        {
            Debug.LogWarning("[ShotgunController] pelletPrefab is not assigned.");
            return;
        }

        Transform cam        = Camera.main.transform;
        Vector3   spawnOrigin = muzzlePoint != null ? muzzlePoint.position
                                                     : cam.position + cam.forward * 0.5f;

        // Collect player colliders so pellets don't self-hit on spawn.
        Collider[] playerColliders = GetComponentsInChildren<Collider>();

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 dir = GetSpreadDirection(cam.forward, cam.right, cam.up);

            GameObject pelletObj = Instantiate(pelletPrefab, spawnOrigin, Quaternion.LookRotation(dir));
            ShotgunPellet pellet = pelletObj.GetComponent<ShotgunPellet>();

            if (pellet == null)
            {
                Debug.LogWarning("[ShotgunController] pelletPrefab is missing ShotgunPellet component.");
                Destroy(pelletObj);
                continue;
            }

            pellet.damage        = pelletDamage;
            pellet.impactPrefab  = bulletImpactPrefab;

            // Ignore all player colliders so the pellet doesn't hit ourselves.
            Collider pelletCol = pelletObj.GetComponent<Collider>();
            if (pelletCol != null)
            {
                foreach (Collider pc in playerColliders)
                    Physics.IgnoreCollision(pelletCol, pc, true);
            }

            pellet.Launch(dir, pelletSpeed);
        }
    }

    /// <summary>
    /// Returns a direction within a cone of half-angle <see cref="spreadAngle"/> around
    /// <paramref name="forward"/>. Uses uniform disk sampling projected onto a sphere.
    /// </summary>
    Vector3 GetSpreadDirection(Vector3 forward, Vector3 right, Vector3 up)
    {
        // Random point on unit disk, scale by tan(half-angle) so edge-of-cone maps
        // to exactly spreadAngle degrees off centre.
        float diskRadius = Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        float angle      = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float r          = Mathf.Sqrt(UnityEngine.Random.Range(0f, 1f)); // sqrt for uniform density

        float offsetX = Mathf.Cos(angle) * r * diskRadius;
        float offsetY = Mathf.Sin(angle) * r * diskRadius;

        return (forward + right * offsetX + up * offsetY).normalized;
    }

    // ─── Reload ──────────────────────────────────────────────────────────────────

    void HandleReload()
    {
        if (_isReloading)                               return;
        if (!_isAiming)                                 return;
        if (!Keyboard.current.rKey.wasPressedThisFrame) return;
        if (_movement != null && _movement.IsRunning)   return;
        if (Time.time < _lastFireTime + fireAnimationDuration) return;
        if (_currentMag == magazineSize) return;
        if (_spareShells <= 0)           return;

        _reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        _isReloading = true;
        _animator.SetTrigger(ReloadHash);

        // Insert one shell at a time until mag is full or spare pool is empty.
        while (_currentMag < magazineSize && _spareShells > 0)
        {
            OnReloadStart?.Invoke();                        // plays shell-insert sound each time
            yield return new WaitForSeconds(reloadTime);   // reloadTime = seconds per shell

            _currentMag++;
            _spareShells--;
            BroadcastAmmo();
        }

        // Fire the appropriate completion event.
        if (_currentMag >= magazineSize)
            OnMagFull?.Invoke();        // mag topped up -- plays "locked and loaded" sound
        else
            OnReloadComplete?.Invoke(); // ran out of spare shells -- no special sound

        _isReloading     = false;
        _reloadCoroutine = null;
    }

    // ─── Death ───────────────────────────────────────────────────────────────────

    void HandlePlayerDied()
    {
        _isDead   = true;
        _isAiming = false;
        if (_animator != null)
            _animator.SetBool(IsAimingHash, false);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    void BroadcastAmmo()
    {
        OnAmmoChanged?.Invoke(_currentMag, _spareShells);
    }
}
