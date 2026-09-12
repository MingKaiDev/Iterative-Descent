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
    [Tooltip("Soft cap used by CombatDDAController to normalise total ammo. The upper end " +
             "of the TotalAmmoNormalised range -- see minAmmoCount below for the lower end.")]
    public int ammoCombatSoftCap = 48;
    [Tooltip("Absolute floor (total rounds -- mag + spare) below which the player is " +
             "considered critically low on ammo, regardless of ammoCombatSoftCap. " +
             "TotalAmmoNormalised treats this as the '0' end of its range instead of true " +
             "zero, and ItemSpawner's ammo-needs check treats hitting this floor as an " +
             "automatic 'needs ammo' regardless of the percentage-of-cap check. Must stay " +
             "below ammoCombatSoftCap.")]
    public int minAmmoCount = 5;

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

    [Header("Barrel Attachment")]
    [Tooltip("Barrel attachment model -- child of weaponModel, assigned in Inspector. Hidden until EquipBarrelAttachment() is called.")]
    public GameObject barrelAttachment;
    [Tooltip("Damage multiplier applied while the barrel attachment is equipped. 1.25 = +25% damage.")]
    public float barrelDamageMultiplier = 1.25f;

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
    [Tooltip("Multiplier on settle speed while the player is moving. 0.3 = 30% as fast.")]
    public float moveSettleRate = 0.3f;

    // --- Public Read-Only State ---------------------------------------------
    public bool  IsReloading => _isReloading;
    public bool  IsAiming    => _isAiming;
    /// <summary>True once the barrel attachment has been equipped (Reagent Routing puzzle reward).</summary>
    public bool  HasBarrelAttachment => _hasBarrelAttachment;
    /// <summary>0 = fresh aim (inaccurate), 1 = fully settled (accurate).</summary>
    // NOTE: the Reagent Routing barrel attachment reward now grants a flat
    // +barrelDamageMultiplier damage bonus (see EquipBarrelAttachment() /
    // HasBarrelAttachment below) -- separate from this TODO.
    // TODO (deferred, discussed for the Reagent Routing barrel attachment reward):
    // additionally tie AccuracyT to bonus damage on FireBullet(), so a tighter/
    // more-settled shot also hits harder on top of the flat barrel bonus.
    // Explicitly put on hold -- not implemented. If picked back up, the natural
    // hook is in FireBullet() right where `damage` is assigned onto the bullet,
    // scaling it by AccuracyT.
    public float AccuracyT   => settleTime > 0f ? Mathf.Clamp01(_aimTimer / settleTime) : 1f;
    public int  CurrentMag  => _currentMag;
    public int  SpareAmmo   => _spareAmmo;

    /// <summary>
    /// [0,1] ammo signal for CombatDDAController. Uses InverseLerp (not a flat ratio) so
    /// the range is (minAmmoCount -> 0, ammoCombatSoftCap -> 1) instead of (0 -> 0) --
    /// matches the same "floor, not raw zero" idiom the time signals already use elsewhere
    /// in this DDA system (e.g. CombatDDAController's fastCombatTime/slowCombatTime).
    /// InverseLerp already clamps to [0,1], so no player state can push this out of range.
    /// </summary>
    public float TotalAmmoNormalised =>
        Mathf.InverseLerp(minAmmoCount, ammoCombatSoftCap, _currentMag + _spareAmmo);

    // --- Private ------------------------------------------------------------
    private Animator       _animator;
    private PlayerMovement _movement;
    private bool           _isAiming;
    private bool           _isReloading;
    private bool           _isDead;
    private bool           _hasBarrelAttachment;
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

        // Barrel attachment starts hidden -- only shown once EquipBarrelAttachment() is called.
        if (barrelAttachment != null) barrelAttachment.SetActive(false);

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
        if (muzzleFlash != null) muzzleFlash.Play();
        OnFired?.Invoke();
        BroadcastAmmo();

        PlayerMetricsTracker.Instance?.NotifyShotFired();
        FireBullet();         // reads AccuracyT -- must fire before timer resets
        _aimTimer = 0f;       // reset after shot so next bullet starts wide again
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

        // Aim toward whatever the crosshair (screen center) is actually pointing at, not just
        // parallel to the camera's forward axis -- see GetCrosshairAimDirection()'s doc comment.
        // No-op today since muzzlePoint is unassigned here (spawnPos already sits on cam.forward),
        // but keeps the pistol correct too if a visible off-axis muzzlePoint is ever wired up.
        Vector3 aimDir = GetCrosshairAimDirection(cam, spawnPos);

        // Spread shrinks as AccuracyT rises (wide on fresh aim, tight when settled).
        float   spread = Mathf.Lerp(maxSpreadAngle, minSpreadAngle, AccuracyT);
        Vector3 dir    = ApplySpread(aimDir, cam.right, cam.up, spread);

        GameObject    bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(dir));
        ShotgunPellet bullet    = bulletObj.GetComponent<ShotgunPellet>();

        if (bullet == null)
        {
            Debug.LogWarning("[PlayerCombat] bulletPrefab is missing a ShotgunPellet component.");
            Destroy(bulletObj);
            return;
        }

        bullet.damage         = _hasBarrelAttachment ? damage * barrelDamageMultiplier : damage;
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

    /// <summary>
    /// Direction from spawnOrigin toward whatever the camera's center (crosshair) is actually
    /// aiming at -- a raycast out along cam.forward, ignoring the Player layer so the ray never
    /// immediately self-hits the player's own collider around the camera. Falls back to a
    /// fixed-distance point along cam.forward if nothing is hit within range. When spawnOrigin
    /// is already on the camera's forward axis (muzzlePoint unassigned), this is equivalent to
    /// cam.forward -- no behaviour change in that case. Same fix as ShotgunController/
    /// RifleController's identical helper -- see either for the full "why" (an off-axis visible
    /// weapon model's muzzlePoint otherwise fires parallel to, but permanently offset from, the
    /// crosshair, since the old code aimed along cam.forward regardless of spawn origin).
    /// </summary>
    static Vector3 GetCrosshairAimDirection(Transform cam, Vector3 spawnOrigin)
    {
        const float aimRayDistance = 500f;
        // Computed here rather than cached in a static field -- LayerMask.GetMask() calls into
        // engine APIs that Unity does not allow from a MonoBehaviour's static field initializer
        // (throws "NameToLayer is not allowed to be called from a MonoBehaviour constructor").
        int nonPlayerLayers = ~LayerMask.GetMask("Player");
        Vector3 aimPoint = Physics.Raycast(cam.position, cam.forward, out RaycastHit aimHit, aimRayDistance, nonPlayerLayers)
            ? aimHit.point
            : cam.position + cam.forward * aimRayDistance;

        Vector3 dir = aimPoint - spawnOrigin;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : cam.forward;
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

    /// <summary>
    /// Public entry point for LevelTransitionManager to re-fire the current ammo count
    /// after a cross-scene level transition. The pistol GameObject survives the scene
    /// load (PersistentPlayer/DontDestroyOnLoad), so OnEnable()'s broadcast only fires
    /// on an actual weapon-switch, not on a scene load of an already-enabled component --
    /// without this, a fresh CombatHUD in the new scene would show stale/default ammo
    /// until the next shot fired or reload. Only meaningful to call while the pistol is
    /// the currently-equipped weapon; see WeaponManager.BroadcastCurrentWeaponState().
    /// </summary>
    public void BroadcastCurrentState() => BroadcastAmmo();

    public void AddAmmo(int amount)
    {
        _spareAmmo += amount;
        BroadcastAmmo();
    }

    /// <summary>
    /// Directly sets magazine + spare ammo (clamped to magazineSize) and broadcasts the
    /// change. Used by CheckpointManager to restore the pistol's ammo on a checkpoint
    /// respawn -- unlike AddAmmo(), this is an absolute set, not an addition.
    /// </summary>
    public void SetAmmo(int mag, int spare)
    {
        _currentMag = Mathf.Clamp(mag, 0, magazineSize);
        _spareAmmo  = Mathf.Max(0, spare);
        BroadcastAmmo();
    }

    /// <summary>
    /// Called by CheckpointManager as part of an in-place checkpoint respawn (no scene
    /// reload) -- clears the dead flag, cancels any in-progress reload so its coroutine
    /// doesn't hang on a WaitUntil that can no longer become true, and restores ammo to
    /// the checkpoint's saved values.
    /// </summary>
    public void Revive(int mag, int spare)
    {
        _isDead      = false;
        _isReloading = false;
        SetAmmo(mag, spare);
    }

    /// <summary>
    /// Permanently reduces settleTime by the given amount (floored at 0.1s so
    /// aiming never becomes instant), making the reticle reach full accuracy
    /// faster while aiming. Intended for one-time permanent upgrade sources
    /// (e.g. the Reagent Routing puzzle's barrel attachment reward) -- unlike
    /// AddAmmo()/Heal(), this is a permanent stat change, not a consumable, so
    /// callers should guard against granting it more than once per session
    /// (see ReagentRoutingEventHandler's _rewardGranted flag for an example).
    /// </summary>
    public void ReduceSettleTime(float amount)
    {
        if (amount <= 0f) return;
        float oldValue = settleTime;
        settleTime = Mathf.Max(0.1f, settleTime - amount);
        Debug.Log($"[PlayerCombat] Barrel attachment applied. Settle time: {oldValue:F2}s -> {settleTime:F2}s.");
    }

    /// <summary>
    /// Permanently equips the barrel attachment: reveals the barrel model (assigned
    /// in Inspector, already parented under weaponModel) and applies
    /// barrelDamageMultiplier to every shot fired from here on. Intended for one-time
    /// permanent upgrade sources (e.g. the Reagent Routing puzzle reward) -- unlike
    /// AddAmmo()/Heal(), this is a permanent stat change, not a consumable, so callers
    /// should guard against granting it more than once per session (see
    /// ReagentRoutingEventHandler's _rewardGranted flag for an example). Stacks
    /// alongside ReduceSettleTime() -- both are granted together by the Reagent
    /// Routing puzzle solve as of 2026-08-30.
    /// </summary>
    public void EquipBarrelAttachment()
    {
        _hasBarrelAttachment = true;
        if (barrelAttachment != null) barrelAttachment.SetActive(true);
        Debug.Log($"[PlayerCombat] Barrel attachment equipped. Damage multiplier: x{barrelDamageMultiplier:F2}.");
    }
}
