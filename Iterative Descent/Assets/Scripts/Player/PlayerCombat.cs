using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    // ─── Events ─────────────────────────────────────────────────────────────────
    // Subscribe to these from HUD, audio, VFX scripts.
    public static event Action<int, int> OnAmmoChanged;  // (currentMag, spareAmmo)
    public static event Action           OnFired;
    public static event Action           OnDryFire;       // trigger pulled on empty mag
    public static event Action           OnReloadStart;
    public static event Action           OnReloadComplete;

    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Pistol — Ammo")]
    public int  magazineSize      = 12;
    public int  startingSpareAmmo = 36;

    [Header("Pistol — Firing")]
    [Tooltip("Minimum seconds between shots (semi-auto feel).")]
    public float fireInterval = 0.2f;
    [Tooltip("Damage per bullet.")]
    public float damage       = 25f;
    [Tooltip("Bullet projectile prefab. Assign the ShotgunPellet prefab -- reused for the pistol.")]
    public GameObject bulletPrefab;
    [Tooltip("Speed of the bullet in m/s. Pistol bullets are faster than shotgun pellets.")]
    public float bulletSpeed  = 60f;
    [Tooltip("Optional: transform at the gun muzzle. Leave unassigned to spawn from camera.")]
    public Transform muzzlePoint;

    [Header("Pistol — DDA")]
    [Tooltip("Soft cap used by CombatDDAController to normalise total ammo. " +
             "Set to the 'comfortable reserve' level determined during playtesting. " +
             "Defaults to magazineSize + startingSpareAmmo.")]
    public int ammoCombatSoftCap = 48;

    [Header("Pistol — Reload")]
    [Tooltip("Seconds the reload animation takes before ammo is refilled.")]
    public float reloadTime = 1.8f;
    [Tooltip("Seconds after firing before a reload is allowed. " +
             "Set this to match the length of your fire animation clip.")]
    public float fireAnimationDuration = 0.5f;

    [Header("Effects (optional)")]
    [Tooltip("Assign the muzzle ParticleSystem on the gun prefab.")]
    public ParticleSystem muzzleFlash;
    [Tooltip("Prefab spawned at bullet hit point.")]
    public GameObject bulletImpactPrefab;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public bool  IsAiming    => _isAiming;
    public bool  IsReloading => _isReloading;
    public int   CurrentMag  => _currentMag;
    public int   SpareAmmo   => _spareAmmo;

    /// <summary>
    /// Total ammo (magazine + spare) normalised against ammoCombatSoftCap.
    /// Used by CombatDDAController as the ammo signal (higher = player has more ammo = skilled).
    /// </summary>
    public float TotalAmmoNormalised =>
        Mathf.Clamp01((float)(_currentMag + _spareAmmo) / Mathf.Max(1, ammoCombatSoftCap));

    // ─── Private ────────────────────────────────────────────────────────────────
    private Animator        _animator;
    private PlayerMovement  _movement;
    private bool            _isAiming;
    private bool            _isReloading;
    private bool            _isDead;
    private int             _currentMag;
    private int             _spareAmmo;
    private float           _nextFireTime;
    private float           _lastFireTime = -999f;  // far in the past so first reload is never blocked

    // Animator parameter hashes
    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int FireHash     = Animator.StringToHash("Fire");
    private static readonly int ReloadHash   = Animator.StringToHash("Reload");

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _animator   = GetComponent<Animator>();
        _movement   = GetComponent<PlayerMovement>();
        _currentMag = magazineSize;
        _spareAmmo  = startingSpareAmmo;

        PlayerHealth.OnPlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
    }

    void OnEnable()
    {
        // Push initial ammo state to HUD on scene load.
        BroadcastAmmo();
    }

    void Update()
    {
        if (_isReloading || _isDead) return;

        // Cancel and block all combat input while any puzzle UI is open.
        if (PlayerInteractor.IsPaused)
        {
            CancelAim();
            return;
        }

        HandleAimToggle();
        HandleFiring();
        HandleReload();
    }

    // ─── Aim ────────────────────────────────────────────────────────────────────

    void HandleAimToggle()
    {
        // Can't enter aim stance while sprinting.
        if (_movement != null && _movement.IsRunning) return;

        if (!Mouse.current.rightButton.wasPressedThisFrame) return;

        _isAiming = !_isAiming;
        _animator.SetBool(IsAimingHash, _isAiming);
    }

    /// <summary>
    /// Force aim off — called by PlayerMovement when sprinting starts mid-aim.
    /// </summary>
    public void CancelAim()
    {
        if (!_isAiming) return;
        _isAiming = false;
        _animator.SetBool(IsAimingHash, false);
    }

    // ─── Fire ───────────────────────────────────────────────────────────────────

    void HandleFiring()
    {
        if (!_isAiming)                                   return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.time < _nextFireTime)                    return;

        _nextFireTime = Time.time + fireInterval;

        // Empty magazine
        if (_currentMag <= 0)
        {
            OnDryFire?.Invoke();
            // TODO: play empty-click audio
            return;
        }

        // Fire
        _currentMag--;
        _lastFireTime = Time.time;
        _animator.SetTrigger(FireHash);
        if (muzzleFlash != null) muzzleFlash.Play();   // Unity null-safe (?. doesn't work with UnityEngine.Object)
        OnFired?.Invoke();
        BroadcastAmmo();

        // Track shot fired for CombatDDA accuracy signal.
        PlayerMetricsTracker.Instance?.NotifyShotFired();

        // Fire a single bullet projectile from the camera centre (correct for 3rd-person crosshair).
        FireBullet();
    }

    void FireBullet()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[PlayerCombat] bulletPrefab is not assigned.");
            return;
        }

        Transform cam      = Camera.main.transform;
        Vector3   spawnPos = muzzlePoint != null ? muzzlePoint.position
                                                 : cam.position + cam.forward * 0.5f;
        Vector3   dir      = cam.forward;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(dir));
        ShotgunPellet bullet = bulletObj.GetComponent<ShotgunPellet>();

        if (bullet == null)
        {
            Debug.LogWarning("[PlayerCombat] bulletPrefab is missing a ShotgunPellet component.");
            Destroy(bulletObj);
            return;
        }

        bullet.damage          = damage;
        bullet.impactPrefab    = bulletImpactPrefab;
        bullet.notifyDDAOnHit  = true;   // single bullet -- count each hit for accuracy DDA

        // Ignore all player colliders so the bullet doesn't self-hit on spawn.
        Collider bulletCol = bulletObj.GetComponent<Collider>();
        if (bulletCol != null)
        {
            foreach (Collider pc in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(bulletCol, pc, true);
        }

        bullet.Launch(dir, bulletSpeed);
    }

    // ─── Reload ─────────────────────────────────────────────────────────────────

    void HandleReload()
    {
        if (!_isAiming)                                 return;   // must be in aim stance to reload
        if (!Keyboard.current.rKey.wasPressedThisFrame) return;
        if (_movement != null && _movement.IsRunning)   return;   // redundant guard (can't aim while running)

        // Block reload until the fire animation has finished playing.
        if (Time.time < _lastFireTime + fireAnimationDuration) return;

        if (_currentMag == magazineSize) return;   // already full
        if (_spareAmmo <= 0)             return;   // no ammo left

        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        _isReloading = true;

        // Stay in aim stance — Reload trigger transitions from Pistol Idle/Walk → Pistol_Reload.
        // Do NOT set IsAiming=false here; that pushes the animator back to base Idle
        // before the trigger fires, so it gets consumed with no effect.
        _animator.SetTrigger(ReloadHash);

        OnReloadStart?.Invoke();

        yield return new WaitForSeconds(reloadTime);

        // Fill magazine from spare pool
        int needed = magazineSize - _currentMag;
        int toLoad = Mathf.Min(needed, _spareAmmo);
        _currentMag += toLoad;
        _spareAmmo  -= toLoad;

        BroadcastAmmo();
        OnReloadComplete?.Invoke();
        _isReloading = false;
    }

    // ─── Death ──────────────────────────────────────────────────────────────────

    void HandlePlayerDied()
    {
        _isDead   = true;
        _isAiming = false;
        _animator.SetBool(IsAimingHash, false);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    void BroadcastAmmo()
    {
        OnAmmoChanged?.Invoke(_currentMag, _spareAmmo);
    }

    /// <summary>
    /// Call from item pickup scripts to add ammo to spare pool.
    /// </summary>
    public void AddAmmo(int amount)
    {
        _spareAmmo += amount;
        BroadcastAmmo();
    }
}
