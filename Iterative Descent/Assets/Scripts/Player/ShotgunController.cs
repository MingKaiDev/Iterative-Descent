using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shotgun weapon controller. Fires N pellets in a spread cone on left click.
/// No aim mode -- gun is always ready to fire.
///
/// Setup:
///   Start this component DISABLED. WeaponManager enables it on pickup.
///   Weapon model should be a child of the Camera, not the player body.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ShotgunController : MonoBehaviour
{
    // --- Events -------------------------------------------------------------
    public static event Action<int, int> OnAmmoChanged;
    public static event Action           OnFired;
    public static event Action           OnDryFire;
    public static event Action           OnReloadStart;
    public static event Action           OnReloadComplete;
    public static event Action           OnMagFull;

    // --- Inspector ----------------------------------------------------------

    [Header("Shotgun - Ammo")]
    public int magazineSize = 6;

    [Header("Shotgun - Firing")]
    public int   pelletCount  = 6;
    public float spreadAngle  = 10f;
    public float pelletSpeed  = 30f;
    public float pelletDamage = 15f;
    public float fireInterval = 0.75f;

    [Header("Shotgun - Reload")]
    [Tooltip("Seconds per shell insert.")]
    public float reloadTime            = 0.65f;
    public float fireAnimationDuration = 0.7f;

    [Header("Prefabs")]
    public GameObject pelletPrefab;
    public Transform  muzzlePoint;

    [Header("Effects (optional)")]
    public ParticleSystem muzzleFlash;
    public GameObject     bulletImpactPrefab;

    [Header("Weapon Model")]
    [Tooltip("Root GameObject of the shotgun mesh. Parented to Camera at runtime.")]
    public GameObject weaponModel;
    [Tooltip("Local position relative to Camera. Tune in Play mode then copy out.")]
    public Vector3 fpsLocalPosition = new Vector3(0.15f, -0.2f, 0.35f);
    [Tooltip("Local rotation relative to Camera.")]
    public Vector3 fpsLocalRotation = new Vector3(0f, 0f, 0f);

    [Header("Reload Tilt")]
    [Tooltip("Euler angle offset applied to the weapon during reload. Z = sideways tilt.")]
    public Vector3 reloadTiltAngle = new Vector3(0f, 0f, 45f);
    [Tooltip("Speed of the tilt in and tilt back transitions.")]
    public float reloadTiltSpeed = 8f;

    // --- Public Read-Only State ---------------------------------------------
    public bool IsAiming    => _isAiming;
    public bool IsReloading => _isReloading;
    public int  CurrentMag  => _currentMag;
    public int  SpareShells => _spareShells;

    // --- Private ------------------------------------------------------------
    private Animator       _animator;
    private PlayerMovement _movement;
    private bool           _isAiming;
    private bool           _isReloading;
    private bool           _isDead;
    private int            _currentMag;
    private int            _spareShells;
    private float          _nextFireTime;
    private float          _lastFireTime = -999f;
    private Coroutine      _reloadCoroutine;

    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int ReloadHash   = Animator.StringToHash("Reload");

    // --- Unity Lifecycle ----------------------------------------------------

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _movement = GetComponent<PlayerMovement>();

        PlayerHealth.OnPlayerDied += HandlePlayerDied;

        // Hide model on startup; OnEnable will show it when the component is activated.
        if (weaponModel != null) weaponModel.SetActive(false);
    }

    void Start()
    {
        if (weaponModel == null) return;

        Transform cam = Camera.main?.transform;
        if (cam == null)
        {
            Debug.LogWarning("[ShotgunController] No main camera found -- cannot parent shotgun model.");
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
        if (_movement != null && _movement.IsRunning)
        {
            CancelAim();
            return;
        }

        if (!Mouse.current.rightButton.wasPressedThisFrame) return;
        _isAiming = !_isAiming;
        _animator.SetBool(IsAimingHash, _isAiming);
    }

    // --- Public API ---------------------------------------------------------

    public void InitAmmo(int mag, int spare)
    {
        _currentMag  = Mathf.Clamp(mag, 0, magazineSize);
        _spareShells = spare;
    }

    public void EquipWithAmmo(int mag, int spare)
    {
        _currentMag  = Mathf.Clamp(mag, 0, magazineSize);
        _spareShells = spare;
        enabled      = true;
    }

    public void CancelAim()
    {
        if (!_isAiming) return;
        _isAiming = false;
        if (_animator != null) _animator.SetBool(IsAimingHash, false);
    }

    public void AddAmmo(int amount)
    {
        _spareShells += amount;
        BroadcastAmmo();
    }

    // --- Fire ---------------------------------------------------------------

    void HandleFiring()
    {
        if (!_isAiming)                                    return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.time < _nextFireTime)                     return;

        // Pump shotguns allow interrupting a reload to fire if a shell is loaded.
        if (_isReloading)
        {
            if (_currentMag <= 0) return;
            if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
            _isReloading     = false;
            _reloadCoroutine = null;
        }

        _nextFireTime = Time.time + fireInterval;

        if (_currentMag <= 0)
        {
            OnDryFire?.Invoke();
            return;
        }

        _currentMag--;
        _lastFireTime = Time.time;
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

        Transform cam         = Camera.main.transform;
        Vector3   spawnOrigin = muzzlePoint != null ? muzzlePoint.position
                                                     : cam.position + cam.forward * 0.5f;

        Collider[] playerColliders = GetComponentsInChildren<Collider>();

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 dir = GetSpreadDirection(cam.forward, cam.right, cam.up);

            GameObject    pelletObj = Instantiate(pelletPrefab, spawnOrigin, Quaternion.LookRotation(dir));
            ShotgunPellet pellet    = pelletObj.GetComponent<ShotgunPellet>();

            if (pellet == null)
            {
                Debug.LogWarning("[ShotgunController] pelletPrefab is missing ShotgunPellet component.");
                Destroy(pelletObj);
                continue;
            }

            pellet.damage       = pelletDamage;
            pellet.impactPrefab = bulletImpactPrefab;

            Collider pelletCol = pelletObj.GetComponent<Collider>();
            if (pelletCol != null)
            {
                foreach (Collider pc in playerColliders)
                    Physics.IgnoreCollision(pelletCol, pc, true);
            }

            pellet.Launch(dir, pelletSpeed);
        }
    }

    Vector3 GetSpreadDirection(Vector3 forward, Vector3 right, Vector3 up)
    {
        float diskRadius = Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        float angle      = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float r          = Mathf.Sqrt(UnityEngine.Random.Range(0f, 1f));

        float offsetX = Mathf.Cos(angle) * r * diskRadius;
        float offsetY = Mathf.Sin(angle) * r * diskRadius;

        return (forward + right * offsetX + up * offsetY).normalized;
    }

    // --- Reload -------------------------------------------------------------

    void HandleReload()
    {
        if (_isReloading)                               return;
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
        StartCoroutine(ReloadTiltRoutine());

        while (_currentMag < magazineSize && _spareShells > 0)
        {
            OnReloadStart?.Invoke();
            yield return new WaitForSeconds(reloadTime);

            _currentMag++;
            _spareShells--;
            BroadcastAmmo();
        }

        if (_currentMag >= magazineSize)
            OnMagFull?.Invoke();
        else
            OnReloadComplete?.Invoke();

        _isReloading     = false;
        _reloadCoroutine = null;
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

        // Hold across all shell insertions until reload finishes or player dies
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
        OnAmmoChanged?.Invoke(_currentMag, _spareShells);
    }
}
