using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bolt-action rifle weapon controller. Fires a single high-damage round on left
/// click while aiming. Modeled directly on ShotgunController's lifecycle/reload
/// pattern (per-round reload loop, hold-RMB-to-fire gate) but with a single
/// projectile per shot instead of a pellet spread, a longer fire delay to sell
/// the manual bolt cycle, and a much longer per-round reload.
///
/// Setup:
///   Start this component DISABLED. WeaponManager enables it on pickup.
///   Weapon model should be a child of the Camera, not the player body.
///   bulletPrefab must carry a ShotgunPellet component (shared bullet/pellet
///   hit-detection class -- see ShotgunPellet.cs) with a Rigidbody + Collider.
/// </summary>
[RequireComponent(typeof(Animator))]
public class RifleController : MonoBehaviour
{
    // --- Events -------------------------------------------------------------
    public static event Action<int, int> OnAmmoChanged;
    public static event Action           OnFired;
    public static event Action           OnDryFire;
    public static event Action           OnReloadStart;
    public static event Action           OnReloadComplete;
    public static event Action           OnMagFull;
    public static event Action           OnCocked;      // played a short delay after firing -- bolt cycle sound

    // --- Inspector ----------------------------------------------------------

    [Header("Rifle - Ammo")]
    [Tooltip("Internal magazine capacity. 5 rounds per the bolt-action design.")]
    public int magazineSize = 5;

    [Header("Rifle - DDA")]
    [Tooltip("Absolute floor on spare rounds below which the player is considered " +
             "critically low. ItemSpawner's rifle-needs check treats hitting this floor as " +
             "an automatic 'needs rounds' regardless of any percentage-based check. " +
             "Replaces the old ItemSpawner-owned rifleNeedsThreshold field -- kept on the " +
             "controller itself so the weapon owns its own ammo semantics, mirroring " +
             "PlayerCombat.minAmmoCount.")]
    public int minAmmoCount = 4;

    [Header("Rifle - Firing")]
    [Tooltip("Damage dealt per round.")]
    public float roundDamage  = 100f;
    [Tooltip("Speed of the round in m/s -- faster than the shotgun pellet.")]
    public float bulletSpeed  = 90f;
    [Tooltip("Minimum seconds between shots. Slightly longer than the shotgun's " +
             "fireInterval (0.75s) to sell the manual bolt cycle -- tune in Inspector.")]
    public float fireInterval = 1f;
    [Tooltip("Cone spread in degrees on fresh aim (AccuracyT = 0).")]
    public float maxSpreadAngle = 1.5f;
    [Tooltip("Cone spread in degrees at full settle (AccuracyT = 1) -- the rifle is the " +
             "precision weapon, so this converges to near-pinpoint, unlike the shotgun's capped settle.")]
    public float minSpreadAngle = 0.2f;

    [Header("Rifle - Aim Accuracy")]
    [Tooltip("Seconds of holding aim before reaching full accuracy (minSpreadAngle). " +
             "Deliberately slow by default -- the bolt-action rifle rewards a steady, held aim.")]
    public float settleTime     = 3f;
    [Tooltip("Multiplier on settle speed while the player is moving. 0.3 = 30% as fast.")]
    public float moveSettleRate = 0.3f;

    [Header("Rifle - Bolt Cycle")]
    [Tooltip("Seconds after firing before the bolt-cock sound (OnCocked event) fires. " +
             "Should land comfortably before fireInterval/fireAnimationDuration elapse.")]
    public float cockDelay = 0.4f;

    [Header("Rifle - Reload")]
    [Tooltip("Seconds per round fed into the magazine. Long, per the bolt-action design.")]
    public float reloadTime            = 1.3f;
    public float fireAnimationDuration = 0.9f;

    [Header("Prefabs")]
    [Tooltip("Bullet prefab -- must carry a ShotgunPellet component.")]
    public GameObject bulletPrefab;
    public Transform  muzzlePoint;

    [Header("Effects (optional)")]
    public ParticleSystem muzzleFlash;
    public GameObject     bulletImpactPrefab;

    [Header("Weapon Model")]
    [Tooltip("Root GameObject of the rifle mesh. Parented to Camera at runtime.")]
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
    public int  SpareRounds => _spareRounds;
    /// <summary>0 = fresh aim (wide spread), 1 = fully settled (near-pinpoint). Slow settleTime by design.</summary>
    public float AccuracyT  => settleTime > 0f ? Mathf.Clamp01(_aimTimer / settleTime) : 1f;

    // --- Private ------------------------------------------------------------
    private Animator       _animator;
    private PlayerMovement _movement;
    private bool           _isAiming;
    private bool           _isReloading;
    private bool           _isDead;
    private int            _currentMag;
    private int            _spareRounds;
    private float          _nextFireTime;
    private float          _lastFireTime = -999f;
    private float          _aimTimer;
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
            Debug.LogWarning("[RifleController] No main camera found -- cannot parent rifle model.");
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
        if (_isAiming)
        {
            bool isMoving = _movement != null && (_movement.IsWalking || _movement.IsRunning);
            float rate    = isMoving ? moveSettleRate : 1f;
            _aimTimer     = Mathf.Min(_aimTimer + Time.deltaTime * rate, settleTime);
        }
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

    // --- Public API ---------------------------------------------------------

    public void InitAmmo(int mag, int spare)
    {
        _currentMag  = Mathf.Clamp(mag, 0, magazineSize);
        _spareRounds = spare;
    }

    /// <summary>
    /// Called by CheckpointManager as part of an in-place checkpoint respawn (no scene
    /// reload) -- clears the dead flag, cancels any in-progress reload, restores ammo
    /// to the checkpoint's saved values, and broadcasts the change (InitAmmo alone does
    /// not broadcast, since it's normally followed by WeaponManager.SetWeapon() doing
    /// that via OnEnable -- no such switch happens here).
    /// </summary>
    public void Revive(int mag, int spare)
    {
        _isDead      = false;
        _isReloading = false;
        InitAmmo(mag, spare);
        OnAmmoChanged?.Invoke(_currentMag, _spareRounds);
    }

    public void EquipWithAmmo(int mag, int spare)
    {
        _currentMag  = Mathf.Clamp(mag, 0, magazineSize);
        _spareRounds = spare;
        enabled      = true;
    }

    public void CancelAim()
    {
        if (!_isAiming) return;
        SetAiming(false);
    }

    public void AddAmmo(int amount)
    {
        _spareRounds += amount;
        BroadcastAmmo();
    }

    // --- Fire ---------------------------------------------------------------

    void HandleFiring()
    {
        if (!_isAiming)                                    return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.time < _nextFireTime)                     return;

        // Mirrors the shotgun's pump-interrupt: firing cancels an in-progress
        // reload as long as a round is already chambered.
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

        PlayerMetricsTracker.Instance?.NotifyShotFired();
        FireRound();               // reads AccuracyT -- must fire before timer resets
        StartCoroutine(PlayCockRoutine());
        _aimTimer = 0f;            // reset after shot so next round's spread starts wide again
    }

    void FireRound()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[RifleController] bulletPrefab is not assigned.");
            return;
        }

        Transform cam         = Camera.main.transform;
        Vector3   spawnOrigin = muzzlePoint != null ? muzzlePoint.position
                                                     : cam.position + cam.forward * 0.5f;

        // Spread narrows as AccuracyT rises -- settleTime is deliberately slow for the rifle.
        float   spread = Mathf.Lerp(maxSpreadAngle, minSpreadAngle, AccuracyT);
        Vector3 dir    = GetSpreadDirection(cam.forward, cam.right, cam.up, spread);

        GameObject    bulletObj = Instantiate(bulletPrefab, spawnOrigin, Quaternion.LookRotation(dir));
        ShotgunPellet round     = bulletObj.GetComponent<ShotgunPellet>();

        if (round == null)
        {
            Debug.LogWarning("[RifleController] bulletPrefab is missing a ShotgunPellet component.");
            Destroy(bulletObj);
            return;
        }

        round.damage         = roundDamage;
        round.impactPrefab   = bulletImpactPrefab;
        round.notifyDDAOnHit = true; // single precision round per shot -- counts toward DDA accuracy same as the pistol

        Collider bulletCol = bulletObj.GetComponent<Collider>();
        if (bulletCol != null)
        {
            foreach (Collider pc in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(bulletCol, pc, true);
        }

        round.Launch(dir, bulletSpeed);
    }

    /// <summary>
    /// Fires OnCocked (the bolt-cycle sound cue) cockDelay seconds after the shot,
    /// so it plays distinctly after the gunshot rather than layered on top of it.
    /// </summary>
    IEnumerator PlayCockRoutine()
    {
        yield return new WaitForSeconds(cockDelay);
        if (_isDead) yield break;
        OnCocked?.Invoke();
    }

    Vector3 GetSpreadDirection(Vector3 forward, Vector3 right, Vector3 up, float angleDeg)
    {
        if (angleDeg <= 0f) return forward;

        float diskRadius = Mathf.Tan(angleDeg * Mathf.Deg2Rad);
        float angle      = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
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
        if (_spareRounds <= 0)           return;

        _reloadCoroutine = StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        CancelAim();   // drop out of aim mode while reloading
        _isReloading = true;
        _animator.SetTrigger(ReloadHash);
        StartCoroutine(ReloadTiltRoutine());

        while (_currentMag < magazineSize && _spareRounds > 0)
        {
            OnReloadStart?.Invoke();
            yield return new WaitForSeconds(reloadTime);

            _currentMag++;
            _spareRounds--;
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

        // Hold across all round insertions until reload finishes or player dies
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
        OnAmmoChanged?.Invoke(_currentMag, _spareRounds);
    }
}
