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

    [Header("Reload Tilt")]
    [Tooltip("Euler angle offset applied to the weapon during reload. Z = sideways tilt.")]
    public Vector3 reloadTiltAngle = new Vector3(0f, 0f, 90f);
    [Tooltip("Speed of the tilt in and tilt back transitions.")]
    public float reloadTiltSpeed = 8f;

    [Header("Aim Accuracy")]
    [Tooltip("Seconds of holding aim before reaching full accuracy.")]
    public float settleTime     = 1.5f;
    [Tooltip("Max bullet spread angle (degrees) on fresh aim.")]
    public float maxSpreadAngle = 6f;
    [Tooltip("Min bullet spread angle (degrees) at full accuracy.")]
    public float minSpreadAngle = 0.3f;

    // --- Public Read-Only State ---------------------------------------------
    public bool  IsReloading => _isReloading;
    public bool  IsAiming    => _isAiming;
    /// <summary>0 = fresh aim (inaccurate), 1 = fully settled (accurate).</summary>
    public float AccuracyT   => settleTime > 0f ? Mathf.Clamp01(_aimTimer / settleTime) : 1f;
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
    private float          _aimTimer;

    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
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

        Transform cam = Camera.main?.transform;
        if (cam == null)
        {
            Debug.LogWarning("[PlayerCombat] No main camera found -- cannot parent pistol model.");
            return;
        }

        weaponModel.transform.SetParent(cam, worldPositionStays: false);
        weaponModel.transform.localPosition = fpsLocalPosition;
        weaponModel.transform.localRotation = Quaternion.Euler(fpsLocalRotation);
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
        // Always hide model when component is disabled (weapon switch away or death).
        if (weaponModel != null) weaponModel.SetActive(false);
    }

    void Update()
    {
        if (_isDead) return;
        if (PlayerInteractor.IsPaused) { CancelAim(); return; }
        if (_isReloading) return;

        HandleAimToggle();
        if (_isAiming) _aimTimer = Mathf.Min(_aimTimer + Time.deltaTime, settleTime);
        HandleFiring();
        HandleReload();
    }

    void HandleAimToggle()
    {
        bool wantsAim = Mouse.current.rightButton.isPressed;
        if (wantsAim != _isAiming)
            SetAiming(wantsAim);
    }

    void SetAiming(bool aim)
    {
        _isAiming = aim;
        _animator.SetBool(IsAimingHash, _isAiming);
        if (!aim) _aimTimer = 0f; // reset settle timer when gun is lowered
    }

    public void CancelAim()
    {
        if (!_isAiming) return;
        SetAiming(false);
    }

    // --- Fire ---------------------------------------------------------------

    void HandleFiring()
    {
        if (!_isAiming)                                    return;
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
        _aimTimer     = 0f;   // recoil breaks settle -- reticle resets on each shot
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

        // Spread shrinks as AccuracyT rises (wide on fresh aim, tight when settled).
        float   spread = Mathf.Lerp(maxSpreadAngle, minSpreadAngle, AccuracyT);
        Vector3 dir    = ApplySpread(cam.forward, cam.right, cam.up, spread);

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

    static Vector3 ApplySpread(Vector3 forward, Vector3 right, Vector3 up, float angleDeg)
    {
        if (angleDeg <= 0f) return forward;
        float disk  = Mathf.Tan(angleDeg * Mathf.Deg2Rad);
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float r     = Mathf.Sqrt(UnityEngine.Random.Range(0f, 1f));
        return (forward + right * (Mathf.Cos(angle) * r * disk)
                        + up    * (Mathf.Sin(angle) * r * disk)).normalized;
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
        CancelAim();   // drop out of aim mode while reloading
        _isReloading = true;
        _animator.SetTrigger(ReloadHash);
        OnReloadStart?.Invoke();
        StartCoroutine(ReloadTiltRoutine());

        yield return new WaitForSeconds(reloadTime);

        int needed  = magazineSize - _currentMag;
        int toLoad  = Mathf.Min(needed, _spareAmmo);
        _currentMag += toLoad;
        _spareAmmo  -= toLoad;

        BroadcastAmmo();
        OnReloadComplete?.Invoke();
        _isReloading = false;
    }

    IEnumerator ReloadTiltRoutine()
    {
        if (weaponModel == null) yield break;

        Quaternion baseRot   = Quaternion.Euler(fpsLocalRotation);
        Quaternion tiltedRot = Quaternion.Euler(fpsLocalRotation + reloadTiltAngle);

        // Tilt out
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * reloadTiltSpeed;
            weaponModel.transform.localRotation = Quaternion.Slerp(baseRot, tiltedRot, Mathf.Clamp01(t));
            yield return null;
        }

        // Hold until reload finishes or player dies
        yield return new WaitUntil(() => !_isReloading || _isDead);

        // Tilt back
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * reloadTiltSpeed;
            weaponModel.transform.localRotation = Quaternion.Slerp(tiltedRot, baseRot, Mathf.Clamp01(t));
            yield return null;
        }

        weaponModel.transform.localRotation = baseRot;
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
