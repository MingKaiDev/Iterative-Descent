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
///                      Ammo needs:   spareAmmo < ammoCombatSoftCap * ammoNeedsThreshold
///                      Health needs: currentHP  < maxHP             * healthNeedsThreshold
///
///   Final chance = Clamp01(baseChance * ddaMult * needsMult)
///
/// ─── One drop per kill ──────────────────────────────────────────────────────
///   If both items roll true in the same event, only the higher-priority drop
///   spawns (health > ammo, because HP loss is more urgent). Disable
///   maxOneDropPerKill in the Inspector to allow both to drop simultaneously.
///
/// ─── Scene setup ────────────────────────────────────────────────────────────
///   Attach to the persistent GameManager GameObject.
///   Assign AmmoBoxPrefab and HealthKitPrefab in the Inspector.
///   Prefabs must have AmmoPickup / HealthPickup + a trigger Collider.
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

    // ─── Inspector: Base Drop Chances ─────────────────────────────────────────

    [Header("Base Drop Chances (before DDA and needs modifiers)")]
    [Range(0f, 1f)]
    [Tooltip("Flat probability an ammo box drops per kill, before any modifiers.")]
    public float ammoBaseChance   = 0.40f;
    [Range(0f, 1f)]
    [Tooltip("Flat probability a health kit drops per kill, before any modifiers.")]
    public float healthBaseChance = 0.25f;

    // ─── Inspector: DDA Modifier ──────────────────────────────────────────────

    [Header("DDA Modifier")]
    [Tooltip("Maximum multiplier applied when DDA score is at its lowest (player struggling). " +
             "1.0 = DDA has no effect on drops.")]
    [Range(1f, 4f)]
    public float ddaDropBonusMax = 2.0f;

    // ─── Inspector: Needs Modifier ────────────────────────────────────────────

    [Header("Needs Modifier")]
    [Range(0f, 1f)]
    [Tooltip("Spare ammo threshold as a fraction of ammoCombatSoftCap. " +
             "Below this level the player is considered to need ammo.")]
    public float ammoNeedsThreshold   = 0.40f;
    [Range(1f, 3f)]
    [Tooltip("Extra multiplier on ammo drop chance when player is below the ammo threshold.")]
    public float ammoNeedsMultiplier  = 1.5f;

    [Range(0f, 1f)]
    [Tooltip("Health threshold as a fraction of maxHealth. " +
             "Below this level the player is considered to need healing.")]
    public float healthNeedsThreshold  = 0.50f;
    [Range(1f, 3f)]
    [Tooltip("Extra multiplier on health drop chance when player is below the health threshold.")]
    public float healthNeedsMultiplier = 1.5f;

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

    private PlayerHealth _playerHealth;
    private PlayerCombat _playerCombat;

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

        if (!dropAmmo && !dropHealth) return;

        if (maxOneDropPerKill)
        {
            // Health is higher priority — more urgent to survive than to have bullets.
            if (dropHealth)
                SpawnItem(healthKitPrefab, deathPosition);
            else
                SpawnItem(ammoBoxPrefab, deathPosition);
        }
        else
        {
            // Allow both to drop on the same kill (rare but possible at high needs + low DDA).
            // Offset the second item slightly so they don't overlap.
            if (dropHealth) SpawnItem(healthKitPrefab, deathPosition);
            if (dropAmmo)   SpawnItem(ammoBoxPrefab,   deathPosition + Vector3.right * 0.3f);
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
    /// Returns ammoNeedsMultiplier when the player's spare ammo is below the threshold.
    /// Falls back to 1.0 if PlayerCombat is not found.
    /// </summary>
    private float AmmoNeedsMultiplier()
    {
        if (_playerCombat == null) return 1f;
        float cap       = Mathf.Max(1, _playerCombat.ammoCombatSoftCap);
        bool  needsAmmo = _playerCombat.SpareAmmo < cap * ammoNeedsThreshold;
        return needsAmmo ? ammoNeedsMultiplier : 1f;
    }

    /// <summary>
    /// Returns healthNeedsMultiplier when the player's HP is below the threshold.
    /// Falls back to 1.0 if PlayerHealth is not found.
    /// </summary>
    private float HealthNeedsMultiplier()
    {
        if (_playerHealth == null) return 1f;
        bool needsHealth = _playerHealth.CurrentHealth < _playerHealth.maxHealth * healthNeedsThreshold;
        return needsHealth ? healthNeedsMultiplier : 1f;
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
        _playerHealth = FindFirstObjectByType<PlayerHealth>();
        _playerCombat = FindFirstObjectByType<PlayerCombat>();

        if (_playerHealth == null)
            Debug.LogWarning("[ItemSpawner] PlayerHealth not found in scene. Health needs check disabled.");
        if (_playerCombat == null)
            Debug.LogWarning("[ItemSpawner] PlayerCombat not found in scene. Ammo needs check disabled.");
    }
}
