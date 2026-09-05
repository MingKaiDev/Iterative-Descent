using UnityEngine;

/// <summary>
/// Handles procedural item drops when enemies are killed.
///
/// ─── Drop Algorithm (three layers) ─────────────────────────────────────────
///
///   1. BASE CHANCE   — configurable per item in the Inspector.
///
///   2. DDA WEIGHT    — reads CombatDDAController.CurrentScore [0=struggling, 1=skilled].
///                      Struggling players get up to ddaDropBonusMax (default 2x) multiplier.
///                      Skilled players get no bonus (1x).
///                      Formula: ddaMult = Lerp(ddaDropBonusMax, 1f, ddaScore)
///
///   3. NEEDS CHECK   — if the player is below a resource threshold, that item's
///                      chance is further multiplied by its needs multiplier (default 1.5x).
///                      Ammo needs (2026-09 revamp -- EITHER can trigger it, since these are
///                      two different ways of detecting the same low-ammo condition):
///                        spareAmmo < ammoCombatSoftCap * ammoNeedsThreshold  OR
///                        spareAmmo <= PlayerCombat.minAmmoCount (absolute floor,
///                        catches a small soft cap where the percentage check alone
///                        could round to an oddly small or zero trigger point)
///                      Health needs (2026-09 revamp -- BOTH must hold, since health kits
///                      are now a capped medkit inventory rather than an instant heal):
///                        currentHP   < maxHP     * healthNeedsThreshold  AND
///                        medkitCount < maxMedkits * medkitNeedsThreshold
///
///   Final chance = Clamp01(baseChance * ddaMult * needsMult)
///
/// ─── One drop per kill ──────────────────────────────────────────────────────
///   If multiple items roll true in the same event, only the higher-priority drop
///   spawns: health > pistol ammo > shotgun shells > rifle rounds (HP loss is most
///   urgent). Disable maxOneDropPerKill in the Inspector to allow all to drop
///   simultaneously.
///
/// ─── Scene setup ────────────────────────────────────────────────────────────
///   Attach to the persistent GameManager GameObject.
///   Assign AmmoBoxPrefab, HealthKitPrefab, ShotgunShellsPrefab, and RifleAmmoBoxPrefab
///   in the Inspector. Shotgun shells and rifle rounds only roll once their
///   respective weapon is unlocked (via WeaponManager).
///   Prefabs must have AmmoPickup / HealthPickup / ShotgunAmmoPickup / RifleAmmoPickup
///   + a trigger Collider.
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────

    public static ItemSpawner Instance { get; private set; }

    // ─── Inspector: Prefabs ───────────────────────────────────────────────────

    [Header("Prefabs")]
    [Tooltip("AmmoBox prefab with AmmoPickup script and trigger Collider.")]
    public GameObject ammoBoxPrefab;
    [Tooltip("HealthKit prefab with HealthPickup script and trigger Collider.")]
    public GameObject healthKitPrefab;
    [Tooltip("ShotgunShells prefab with ShotgunAmmoPickup script and trigger Collider.")]
    public GameObject shotgunShellsPrefab;
    [Tooltip("RifleAmmoBox prefab with RifleAmmoPickup script and trigger Collider. Only rolls if rifle is unlocked.")]
    public GameObject rifleAmmoBoxPrefab;

    // ─── Inspector: Base Drop Chances ─────────────────────────────────────────

    [Header("Base Drop Chances (before DDA and needs modifiers)")]
    [Range(0f, 1f)]
    [Tooltip("Flat probability an ammo box drops per kill, before any modifiers.")]
    public float ammoBaseChance   = 0.40f;
    [Range(0f, 1f)]
    [Tooltip("Flat probability a health kit drops per kill, before any modifiers.")]
    public float healthBaseChance = 0.25f;
    [Range(0f, 1f)]
    [Tooltip("Flat probability shotgun shells drop per kill. Only rolls if shotgun is unlocked.")]
    public float shellBaseChance  = 0.30f;
    [Range(0f, 1f)]
    [Tooltip("Flat probability a rifle ammo box drops per kill. Only rolls if rifle is unlocked.")]
    public float rifleBaseChance  = 0.30f;

    // ─── Inspector: DDA Modifier ──────────────────────────────────────────────

    [Header("DDA Modifier")]
    [Tooltip("Maximum multiplier applied when DDA score is at its lowest (player struggling). " +
             "1.0 = DDA has no effect on drops.")]
    [Range(1f, 4f)]
    public float ddaDropBonusMax = 2.0f;

    // ─── Inspector: Needs Modifier ────────────────────────────────────────────

    [Header("Needs Modifier")]
    [Range(0f, 1f)]
    [Tooltip("Spare ammo threshold as a fraction of ammoCombatSoftCap. Below this level " +
             "(OR below PlayerCombat.minAmmoCount, see that field's tooltip) the player is " +
             "considered to need ammo.")]
    public float ammoNeedsThreshold   = 0.40f;
    [Range(1f, 3f)]
    [Tooltip("Extra multiplier on ammo drop chance when either ammo-needs condition above is met.")]
    public float ammoNeedsMultiplier  = 1.5f;

    [Range(0f, 1f)]
    [Tooltip("Health threshold as a fraction of maxHealth. " +
             "Below this level (AND below medkitNeedsThreshold, see below) the player is " +
             "considered to need healing.")]
    public float healthNeedsThreshold  = 0.50f;
    [Range(0f, 1f)]
    [Tooltip("Held-medkit threshold as a fraction of PlayerHealth.maxMedkits. Below this level " +
             "(AND below healthNeedsThreshold on current HP) the player is considered to need " +
             "healing. Both conditions must hold -- a player sitting on a full medkit stack " +
             "doesn't need more dropped just because they're currently hurt, and a player who's " +
             "out of medkits but at high HP doesn't need one dropped yet either.")]
    public float medkitNeedsThreshold  = 0.50f;
    [Range(1f, 3f)]
    [Tooltip("Extra multiplier on health drop chance when both needs conditions above are met.")]
    public float healthNeedsMultiplier = 1.5f;

    [Range(1f, 3f)]
    [Tooltip("Extra multiplier on shell drop chance when spare shells are at or below " +
             "ShotgunController.minAmmoCount.")]
    public float shellNeedsMultiplier  = 1.5f;

    [Range(1f, 3f)]
    [Tooltip("Extra multiplier on rifle ammo drop chance when spare rounds are at or below " +
             "RifleController.minAmmoCount.")]
    public float rifleNeedsMultiplier  = 1.5f;

    // ─── Inspector: Spawn Settings ────────────────────────────────────────────

    [Header("Spawn Settings")]
    [Tooltip("When true, at most one item drops per kill. Health takes priority over ammo " +
             "if both would drop. Prevents item flooding.")]
    public bool maxOneDropPerKill = true;
    [Tooltip("Items spawn this many units above the death position to avoid floor clipping.")]
    public float spawnHeightOffset = 0.3f;
    [Tooltip("Random XZ radius around the death position. Prevents stacking when enemies die close together.")]
    public float spawnScatterRadius = 0.4f;

    // ─── Private ──────────────────────────────────────────────────────────────

    private PlayerHealth      _playerHealth;
    private PlayerCombat      _playerCombat;
    private ShotgunController _shotgun;
    private RifleController   _rifle;
    private WeaponManager     _weaponManager;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        CachePlayerRefs();
    }

    private void OnEnable()
    {
        EnemyBase.OnAnyEnemyDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        EnemyBase.OnAnyEnemyDied -= HandleEnemyDied;
    }

    // ─── Event Handler ────────────────────────────────────────────────────────

    private void HandleEnemyDied(Vector3 deathPosition)
    {
        // Re-cache if lost (e.g. scene reload)
        if (_playerHealth == null || _playerCombat == null)
            CachePlayerRefs();

        bool dropAmmo   = RollAmmo();
        bool dropHealth = RollHealth();
        bool dropShells = RollShotgunShells();
        bool dropRifle  = RollRifleRounds();

        if (!dropAmmo && !dropHealth && !dropShells && !dropRifle) return;

        if (maxOneDropPerKill)
        {
            // Priority: health > pistol ammo > shotgun shells > rifle rounds.
            if (dropHealth)
                SpawnItem(healthKitPrefab,    deathPosition);
            else if (dropAmmo)
                SpawnItem(ammoBoxPrefab,      deathPosition);
            else if (dropShells)
                SpawnItem(shotgunShellsPrefab, deathPosition);
            else
                SpawnItem(rifleAmmoBoxPrefab, deathPosition);
        }
        else
        {
            // Allow multiple drops on the same kill. Offset so they don't stack.
            if (dropHealth) SpawnItem(healthKitPrefab,     deathPosition);
            if (dropAmmo)   SpawnItem(ammoBoxPrefab,       deathPosition + Vector3.right * 0.3f);
            if (dropShells) SpawnItem(shotgunShellsPrefab, deathPosition - Vector3.right * 0.3f);
            if (dropRifle)  SpawnItem(rifleAmmoBoxPrefab,  deathPosition + Vector3.forward * 0.3f);
        }
    }

    // ─── Roll Methods ─────────────────────────────────────────────────────────

    private bool RollAmmo()
    {
        if (ammoBoxPrefab == null) return false;

        float ddaMult   = DDAMultiplier();
        float needsMult = AmmoNeedsMultiplier();
        float chance    = Mathf.Clamp01(ammoBaseChance * ddaMult * needsMult);

        bool drop = Random.value < chance;
        Debug.Log($"[ItemSpawner] Ammo roll: base={ammoBaseChance:F2} dda={ddaMult:F2} " +
                  $"needs={needsMult:F2} final={chance:F2} -> {(drop ? "DROP" : "no drop")}");
        return drop;
    }

    private bool RollHealth()
    {
        if (healthKitPrefab == null) return false;

        float ddaMult   = DDAMultiplier();
        float needsMult = HealthNeedsMultiplier();
        float chance    = Mathf.Clamp01(healthBaseChance * ddaMult * needsMult);

        bool drop = Random.value < chance;
        Debug.Log($"[ItemSpawner] Health roll: base={healthBaseChance:F2} dda={ddaMult:F2} " +
                  $"needs={needsMult:F2} final={chance:F2} -> {(drop ? "DROP" : "no drop")}");
        return drop;
    }

    private bool RollShotgunShells()
    {
        if (shotgunShellsPrefab == null) return false;
        if (_weaponManager == null || !_weaponManager.IsShotgunUnlocked) return false;

        float ddaMult   = DDAMultiplier();
        float needsMult = ShellNeedsMultiplier();
        float chance    = Mathf.Clamp01(shellBaseChance * ddaMult * needsMult);

        bool drop = Random.value < chance;
        Debug.Log($"[ItemSpawner] Shell roll: base={shellBaseChance:F2} dda={ddaMult:F2} " +
                  $"needs={needsMult:F2} final={chance:F2} -> {(drop ? "DROP" : "no drop")}");
        return drop;
    }

    private bool RollRifleRounds()
    {
        if (rifleAmmoBoxPrefab == null) return false;
        if (_weaponManager == null || !_weaponManager.IsRifleUnlocked) return false;

        float ddaMult   = DDAMultiplier();
        float needsMult = RifleNeedsMultiplier();
        float chance    = Mathf.Clamp01(rifleBaseChance * ddaMult * needsMult);

        bool drop = Random.value < chance;
        Debug.Log($"[ItemSpawner] Rifle roll: base={rifleBaseChance:F2} dda={ddaMult:F2} " +
                  $"needs={needsMult:F2} final={chance:F2} -> {(drop ? "DROP" : "no drop")}");
        return drop;
    }

    // ─── Public Guaranteed Spawn ──────────────────────────────────────────────

    /// <summary>
    /// Unconditionally spawns one ammo box AND one health kit at the given position.
    /// Use for puzzle reward terminals where the drop is not chance-based.
    /// </summary>
    public void GuaranteedSpawn(Vector3 position)
    {
        SpawnItem(ammoBoxPrefab,   position);
        SpawnItem(healthKitPrefab, position + Vector3.right * 0.5f);
    }

    // ─── Modifier Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns a multiplier in [1, ddaDropBonusMax].
    /// Low DDA score (player struggling) → high multiplier.
    /// High DDA score (player skilled)   → multiplier of 1.
    /// Falls back to 1.0 if CombatDDAController is not present.
    /// </summary>
    private float DDAMultiplier()
    {
        if (CombatDDAController.Instance == null) return 1f;
        float score = CombatDDAController.Instance.CurrentScore; // [0,1]
        return Mathf.Lerp(ddaDropBonusMax, 1f, score);
    }

    /// <summary>
    /// Returns ammoNeedsMultiplier when the player's spare ammo is below the percentage
    /// threshold OR at/below PlayerCombat's absolute minAmmoCount floor -- either condition
    /// is enough, since they're two different ways of catching the same "running low" state
    /// (the absolute floor matters most for a weapon with a small soft cap, where a flat
    /// percentage could round down to an unreachable trigger point).
    /// Falls back to 1.0 if PlayerCombat is not found.
    /// </summary>
    private float AmmoNeedsMultiplier()
    {
        if (_playerCombat == null) return 1f;

        float cap                  = Mathf.Max(1, _playerCombat.ammoCombatSoftCap);
        bool  belowPercentThreshold = _playerCombat.SpareAmmo < cap * ammoNeedsThreshold;
        bool  belowAbsoluteFloor    = _playerCombat.SpareAmmo <= _playerCombat.minAmmoCount;

        return (belowPercentThreshold || belowAbsoluteFloor) ? ammoNeedsMultiplier : 1f;
    }

    /// <summary>
    /// Returns healthNeedsMultiplier only when the player is BOTH low on current HP
    /// AND low on held medkits -- see the class doc comment for why both are required.
    /// Falls back to 1.0 if PlayerHealth is not found.
    /// </summary>
    private float HealthNeedsMultiplier()
    {
        if (_playerHealth == null) return 1f;

        bool lowHp      = _playerHealth.CurrentHealth < _playerHealth.maxHealth * healthNeedsThreshold;
        bool lowMedkits = _playerHealth.MedkitCount    < _playerHealth.maxMedkits * medkitNeedsThreshold;

        return (lowHp && lowMedkits) ? healthNeedsMultiplier : 1f;
    }

    /// <summary>
    /// Returns shellNeedsMultiplier when spare shells are at or below
    /// ShotgunController.minAmmoCount. Falls back to 1.0 if ShotgunController is not found.
    /// </summary>
    private float ShellNeedsMultiplier()
    {
        if (_shotgun == null) return 1f;
        bool needsShells = _shotgun.SpareShells <= _shotgun.minAmmoCount;
        return needsShells ? shellNeedsMultiplier : 1f;
    }

    /// <summary>
    /// Returns rifleNeedsMultiplier when spare rounds are at or below
    /// RifleController.minAmmoCount. Falls back to 1.0 if RifleController is not found.
    /// </summary>
    private float RifleNeedsMultiplier()
    {
        if (_rifle == null) return 1f;
        bool needsRounds = _rifle.SpareRounds <= _rifle.minAmmoCount;
        return needsRounds ? rifleNeedsMultiplier : 1f;
    }

    // ─── Spawn ────────────────────────────────────────────────────────────────

    private void SpawnItem(GameObject prefab, Vector3 origin)
    {
        if (prefab == null) return;

        // Small random XZ scatter so items don't stack on top of each other.
        Vector2 scatter = Random.insideUnitCircle * spawnScatterRadius;
        Vector3 spawnPos = origin + new Vector3(scatter.x, spawnHeightOffset, scatter.y);

        Instantiate(prefab, spawnPos, Quaternion.identity);
        Debug.Log($"[ItemSpawner] Spawned '{prefab.name}' at {spawnPos}.");
    }

    // ─── Internal ─────────────────────────────────────────────────────────────

    private void CachePlayerRefs()
    {
        _playerHealth  = FindFirstObjectByType<PlayerHealth>();
        _playerCombat  = FindFirstObjectByType<PlayerCombat>();
        _shotgun       = FindFirstObjectByType<ShotgunController>();
        _rifle         = FindFirstObjectByType<RifleController>();
        _weaponManager = FindFirstObjectByType<WeaponManager>();

        if (_playerHealth  == null)
            Debug.LogWarning("[ItemSpawner] PlayerHealth not found in scene. Health needs check disabled.");
        if (_playerCombat  == null)
            Debug.LogWarning("[ItemSpawner] PlayerCombat not found in scene. Ammo needs check disabled.");
        if (_shotgun       == null)
            Debug.LogWarning("[ItemSpawner] ShotgunController not found in scene. Shell needs check disabled.");
        if (_rifle         == null)
            Debug.LogWarning("[ItemSpawner] RifleController not found in scene. Rifle needs check disabled.");
        if (_weaponManager == null)
            Debug.LogWarning("[ItemSpawner] WeaponManager not found in scene. Weapon unlock gates disabled.");
    }
}
