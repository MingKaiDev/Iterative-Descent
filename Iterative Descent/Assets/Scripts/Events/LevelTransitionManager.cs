// LevelTransitionManager.cs
// Self-instantiating singleton (same pattern as CheckpointManager/PlayerMetricsTracker)
// that loads a new level scene and carries the player's current state into it.
//
// "Carries state over" here means almost nothing has to be copied by hand: the Player
// GameObject itself survives the scene load via PersistentPlayer (DontDestroyOnLoad),
// so health, medkits, ammo for every weapon, which weapons are unlocked, the currently
// equipped weapon, and any permanent upgrades (barrel attachment, settle-time reduction)
// are all still sitting on the exact same component instances when the new scene finishes
// loading -- nothing to snapshot/restore, unlike CheckpointManager's same-scene respawn.
// DDA state (PlayerMetricsTracker) and the checkpoint system (CheckpointManager) are
// already their own DontDestroyOnLoad singletons, so they carry over automatically too.
// Static event flags used to gate puzzle/story beats (e.g.
// PacketFilterEventHandler.ServerNodeDisabled) are plain C# statics -- they survive a
// scene load on their own, nothing to do there either.
//
// What this class actually does, since the player/data side takes care of itself:
//   1. Loads the target scene (SceneManager.LoadScene).
//   2. Once it's loaded, finds the LevelSpawnPoint matching spawnPointId in the new
//      scene and teleports the persisted player there (PlayerMovement.TeleportTo).
//   3. Re-fires the "on spawn" event and the HUD broadcasts that only fire on an actual
//      stat *change* or a weapon-switch -- since the player survived the load rather
//      than being freshly instantiated, none of those would otherwise fire again, and
//      the new scene's fresh Canvas/HUD would be left showing default/blank values.
//
// Known gap, not handled here: PlayerMetricsTracker's "current room" tracking is driven
// by RoomTrigger's OnTriggerEnter/Exit. Leaving Level 1 via this transition does not fire
// an OnTriggerExit for whatever room the player left from (the scene just unloads), so
// PlayerMetricsTracker may still think it's in that old room until Level 2's own
// RoomTriggers are walked into. Not fixed here -- Level 2 has no rooms yet to test
// against, and PlayerMetricsTracker is a large, separate system; worth a dedicated look
// once Level 2's RoomTriggers exist.
//
// Setup:
//   1. Nothing to place in the scene for this manager itself -- same "Instance creates
//      itself on first access" convention as CheckpointManager/PlayerMetricsTracker.
//   2. The Player's root GameObject needs a PersistentPlayer component (see that file).
//   3. Every scene that can be a transition target needs at least one LevelSpawnPoint
//      (see that file) placed where the player should appear.
//   4. The target scene must be added to File > Build Settings > Scenes In Build --
//      SceneManager.LoadScene(string) looks it up by that list, not just by file path.
//   5. Call LevelTransitionManager.Instance.TransitionToScene("Level 2") from wherever
//      the transition should fire -- see LevelTransitionDoorProp.cs.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransitionManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────────────────────
    public static LevelTransitionManager Instance
    {
        get
        {
            if (_instance == null && !_quitting)
            {
                var go = new GameObject("LevelTransitionManager (auto)");
                _instance = go.AddComponent<LevelTransitionManager>();
            }
            return _instance;
        }
    }
    private static LevelTransitionManager _instance;
    private static bool _quitting;

    [Header("Respawn")]
    [Tooltip("Tag on the Player GameObject, used to find it again once the new scene has loaded.")]
    public string playerTag = "Player";

    // ─── Private ────────────────────────────────────────────────────────────────
    private bool   _transitionPending;
    private string _pendingSceneName;
    private string _pendingSpawnPointId;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnApplicationQuit() { _quitting = true; }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads sceneName and, once it's finished loading, teleports the persisted player
    /// to the LevelSpawnPoint in that scene whose spawnId matches spawnPointId (defaults
    /// to "Default" -- every target scene needs at least one LevelSpawnPoint with that id
    /// unless a non-default id is deliberately used for a multi-entry-point scene).
    /// </summary>
    public void TransitionToScene(string sceneName, string spawnPointId = "Default")
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[LevelTransitionManager] TransitionToScene called with no scene name.");
            return;
        }

        _transitionPending   = true;
        _pendingSceneName    = sceneName;
        _pendingSpawnPointId = spawnPointId;

        // A dialogue line (e.g. ARBITEX mid-speech) has no guarantee of surviving a
        // scene load cleanly -- DialogueManager persists fine as an object, but a
        // coroutine suspended mid-yield when the scene underneath it changes has
        // nowhere reliable to resume "into". Rather than let a transition happen
        // mid-line and risk it getting stuck (reported: dialogue "does not dismiss"
        // specifically when transitioning during dialogue), end it deterministically
        // right here, every time, before the load fires. See DialogueManager.EndImmediate().
        DialogueManager.Instance?.EndImmediate();

        Debug.Log($"[LevelTransitionManager] Loading '{sceneName}' (spawn point '{spawnPointId}')...");
        SceneManager.LoadScene(sceneName);
    }

    // ─── Scene Loaded Handler ───────────────────────────────────────────────────

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Guard against reacting to an unrelated scene load (e.g. a full checkpoint-less
        // restart reloading Level 1 itself) -- only act on the specific load this manager
        // actually requested.
        if (!_transitionPending || scene.name != _pendingSceneName) return;
        _transitionPending = false;

        StartCoroutine(PlacePlayerNextFrame());
    }

    /// <summary>
    /// Waits one frame before placing the player -- lets every object in the newly
    /// loaded scene (LevelSpawnPoint included) finish its own Awake/OnEnable first.
    /// </summary>
    private IEnumerator PlacePlayerNextFrame()
    {
        yield return null;

        var spawnPoint = FindSpawnPoint(_pendingSpawnPointId);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[LevelTransitionManager] No LevelSpawnPoint with id '{_pendingSpawnPointId}' " +
                              $"found in '{_pendingSceneName}' -- player was not moved.");
            yield break;
        }

        var player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            Debug.LogWarning($"[LevelTransitionManager] No GameObject tagged '{playerTag}' found -- " +
                              "is PersistentPlayer attached to the Player's root GameObject?");
            yield break;
        }

        var movement = player.GetComponent<PlayerMovement>();
        var health   = player.GetComponent<PlayerHealth>();
        var weapons  = player.GetComponent<WeaponManager>();

        movement?.TeleportTo(spawnPoint.transform.position, spawnPoint.transform.eulerAngles.y);

        // Re-fire the broadcasts that only update on an actual change/switch -- the
        // player survived the load rather than being freshly instantiated, so the new
        // scene's fresh HUD would otherwise show default/blank values until the next
        // real health change, ammo change, or weapon switch. See each method's own doc
        // comment (PlayerHealth/PlayerCombat/ShotgunController/RifleController/WeaponManager).
        health?.BroadcastCurrentState();
        weapons?.BroadcastCurrentWeaponState();
        movement?.NotifySpawned();

        Debug.Log($"[LevelTransitionManager] Player placed at spawn point '{_pendingSpawnPointId}' in '{_pendingSceneName}'.");
    }

    private static LevelSpawnPoint FindSpawnPoint(string spawnId)
    {
        var points = Object.FindObjectsByType<LevelSpawnPoint>(FindObjectsSortMode.None);
        foreach (var p in points)
        {
            if (p.spawnId == spawnId) return p;
        }
        return null;
    }
}
