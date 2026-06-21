using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    // --- Events -------------------------------------------------------------
    public static event Action<int, int> OnAmmoChanged;
    public static event Action           OnFired;
    public static event Action           OnDryFire;
    public static event Action           OnReloadStart;
    public static event Action           OnReloadComplete;

    // --- Inspector ----------------------------------------------------------

    [Header("Pistol - Ammo")]
    public int magazineSize      = 12;
    public int startingSpareAmmo = 36;

    [Header("Pistol - Firing")]
    [Tooltip("Minimum seconds between shots (semi-auto feel).")]
    public float fireInterval = 0.2f;
    [Tooltip("Damage per bullet.")]
    public float damage       = 25f;
    [Tooltip("Bullet projectile prefab.")]
    public GameObject bulletPrefab;
    [Tooltip("Speed of the bullet in m/s.")]
    public float bulletSpeed  = 60f;
    [Tooltip("Optional: transform at the gun muzzle. Leave unassigned to spawn from camera.")]
    public Transform muzzlePoint;

    [Header("Pistol - DDA")]
    [Tooltip("Soft cap used by CombatDDAController to normalise total ammo.")]
    public int ammoCombatSoftCap = 48;

    [Header("Pistol - Reload")]
    [Tooltip("Seconds the reload animation takes before ammo is refilled.")]
    public float reloadTime = 1.8f;
    [Tooltip("Seconds after firing before a reload is allowed.")]
    public float fireAnimationDuration = 0.5f;

    [Header("Effects (optional)")]
    public ParticleSystem muzzleFlash;
    public GameObject     bulletImpactPrefab;

    [Header("Weapon Model")]
    [Tooltip("Root GameObject of the pistol mesh. Parented to Camera at runtime.")]
    public GameObject weaponModel;
    [Tooltip("Local position relative to Camera. Tune in Play mode then copy out.")]
    public Vector3 fpsLocalPosition = new Vector3(0.15f, -0.2f, 0.35f);
    [Tooltip("Local rotation relative to Camera.")]
    public Vector3 fpsLocalRotation = new Vector3(0f, 0f, 0f);

    // --- Public Read-Only State ---------------------------------------------
    public bool IsReloading => _isReloading;
    public bool IsAiming    => _isAiming;
    public int  CurrentMag  => _currentMag;
    public int  SpareAmmo   => _spareAmmo;

    public float TotalAmmoNormalised =>
        Mathf.Clamp01((float)(_currentMag + _spareAmmo) / Mathf.Max(1, ammoCombatSoftCap));

    // --- Private ------------------------------------------------------------
    private Animator       _animator;
    private PlayerMovement _movement;
    private bool           _isAiming;
    private bool           _isReloading;
    private bool           _isDead;
    private int            _currentMag;
    private int            _spareAmmo;
    private float          _nextFireTime;
    private float          _lastFireTime = -999f;

    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int FireHash     = Animator.StringToHash("Fire");
    private static readonly int ReloadHash   = Animator.StringToHash("Reload");

    // --- Unity Lifecycle ----------------------------------------------------

    void Awake()
    {
        _animator   = GetComponent<Animator>();
        _movement   = GetComponent<PlayerMovement>();
        _currentMag = magazineSize;
        _spareAmmo  = startingSpareAmmo;

        // Hide model on startup; OnEnable will show it once the component is active.
        if (weaponModel != null) weaponModel.SetActive(false);

        PlayerHealth.OnPlayerDied += HandlePlayerDied;
    }

    void Start()
    {
        if (weaponModel == null) return;

        Transform handBone = FindBone("mixamorig:RightHand");
        if (handBone == null)
        {
            Debug.LogWarning("[PlayerCombat] mixamorig:RightHand not found -- cannot parent pistol model.");
            return;
        }

        weaponModel.transform.SetParent(handBone, worldPositionStays: false);
        weaponModel.transform.localPosition = fpsLocalPosition;
        weaponModel.transform.localRotation = Quaternion.Euler(fpsLocalRotation);
    }

    Transform FindBone(string boneName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(includeInactive: true))
            if (t.name == boneName) return t;
        return null;
    }

    void OnDestroy()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
    }

    void OnEnable()
    {
        if (weaponModel != null) weaponModel.SetActive(true);
        BroadcastAmmo();
    }

    void OnDisable()
    {
        if (weaponModel != null) weaponModel.SetActive(false);
    }

    void Update()
    {
        if (_isDead) return;
        if (PlayerInteractor.IsPaused) { CancelAim(); return; }
        if (_isReloading) return;

        HandleAimToggle();
        HandleFiring();
        HandleReload();
    }

    void HandleAimToggle()
    {
        // Sprint cancels aim so the run animation doesn't conflict.
        if (_movement != null && _movement.IsRunning)
        {
            CancelAim();
            return;
        }

        if (!Mouse.current.rightButton.wasPressedThisFrame) return;
        _isAiming = !_isAiming;
        _animator.SetBool(IsAimingHash, _isAiming);
    }

    public void CancelAim()
    {
        if (!_isAiming) return;
        _isAiming = false;
        _animator.SetBool(IsAimingHash, false);
    }

    // --- Fire ---------------------------------------------------------------

    void HandleFiring()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.time < _nextFireTime)                     return;

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

        PlayerMetricsTracker.Instance?.NotifyShotFired();
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

        GameObject    bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(dir));
        ShotgunPellet bullet    = bulletObj.GetComponent<ShotgunPellet>();

        if (bullet == null)
        {
            Debug.LogWarning("[PlayerCombat] bulletPrefab is missing a ShotgunPellet component.");
            Destroy(bulletObj);
            return;
        }

        bullet.damage         = damage;
        bullet.impactPrefab   = bulletImpactPrefab;
        bullet.notifyDDAOnHit = true;

        Collider bulletCol = bulletObj.GetComponent<Collider>();
        if (bulletCol != null)
        {
            foreach (Collider pc in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(bulletCol, pc, true);
        }

        bullet.Launch(dir, bulletSpeed);
    }

    // --- Reload -------------------------------------------------------------

    void HandleReload()
    {
        if (!Keyboard.current.rKey.wasPressedThisFrame) return;
        if (_movement != null && _movement.IsRunning)   return;
        if (Time.time < _lastFireTime + fireAnimationDuration) return;
        if (_currentMag == magazineSize) return;
        if (_spareAmmo <= 0)             return;

        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        _isReloading = true;
        _animator.SetTrigger(ReloadHash);
        OnReloadStart?.Invoke();

        yield return new WaitForSeconds(reloadTime);

        int needed  = magazineSize - _currentMag;
        int toLoad  = Mathf.Min(needed, _spareAmmo);
        _currentMag += toLoad;
        _spareAmmo  -= toLoad;

        BroadcastAmmo();
        OnReloadComplete?.Invoke();
        _isReloading = false;
    }

    // --- Death --------------------------------------------------------------

    void HandlePlayerDied()
    {
        _isDead = true;
        CancelAim();
    }

    // --- Helpers ------------------------------------------------------------

    void BroadcastAmmo()
    {
        OnAmmoChanged?.Invoke(_currentMag, _spareAmmo);
    }

    public void AddAmmo(int amount)
    {
        _spareAmmo += amount;
        BroadcastAmmo();
    }
}
