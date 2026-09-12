using System;
using UnityEngine;

/// <summary>
/// Tracks the player's most recent checkpoint (health, medkits, and every unlocked
/// weapon's ammo) and handles an in-place respawn on death -- no scene reload, so
/// puzzle progress, unlocked doors, and already-dead enemies are all untouched.
///
/// Self-instantiating singleton (same pattern as PlayerMetricsTracker): the first
/// access to Instance creates a persistent GameObject automatically, so no manual
/// scene wiring is needed for the manager itself. Only CheckpointTrigger needs to be
/// placed by hand at each checkpoint location.
///
/// Design intent (per project decision, 2026-09-09, revised same day): the "YOU DIED"
/// screen (DeathScreenUI / CombatHUD) always shows on death, same as before this system
/// existed -- this class does NOT auto-respawn. Its Restart/Respawn button now branches:
/// if HasCheckpoint is true, it calls RespawnPlayer() below (in-place respawn, no scene
/// reload); otherwise it falls through to the original full scene reload. So the very
/// first encounter (no checkpoint yet) still restarts the whole game when the player
/// presses that button, exactly as before -- only the meaning of the button past the
/// first checkpoint has changed. See DeathScreenUI.RestartGame() / CombatHUD.RestartScene().
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────────────────────
    public static CheckpointManager Instance
    {
        get
        {
            if (_instance == null && !_quitting)
            {
                var go = new GameObject("CheckpointManager (auto)");
                _instance = go.AddComponent<CheckpointManager>();
            }
            return _instance;
        }
    }
    private static CheckpointManager _instance;
    private static bool _quitting;

    // ─── Inspector ──────────────────────────────────────────────────────────────
    [Header("Respawn")]
    [Tooltip("Tag on the Player GameObject, used to find it again for respawn.")]
    public string playerTag = "Player";

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public bool HasCheckpoint { get; private set; }

    public static event Action OnCheckpointSaved;

    // ─── Private ────────────────────────────────────────────────────────────────
    [Serializable]
    private class Snapshot
    {
        public Vector3 position;
        public float   yaw;
        public float   health;
        public int     medkitCount;
        public int     pistolMag;
        public int     pistolSpare;
        public bool    shotgunUnlocked;
        public int     shotgunMag;
        public int     shotgunSpare;
        public bool    rifleUnlocked;
        public int     rifleMag;
        public int     rifleSpare;
        public EncounterTrigger[] encountersToReset;
        public BossStateMachine   bossToReset;
        public DeimosStateMachine deimosToReset;
        public EncounterTrigger   phobosToReset;
    }
    private Snapshot _snap;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnApplicationQuit() { _quitting = true; }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by CheckpointTrigger when the player walks over a checkpoint. Snapshots
    /// current health, medkits, and every currently-unlocked weapon's ammo, plus the
    /// respawn point's position/facing. Safe to call repeatedly (e.g. re-entering the
    /// same trigger later) -- each call simply overwrites the saved snapshot with the
    /// player's current stats at that moment, which is the intended behaviour.
    /// </summary>
    public void SaveCheckpoint(Transform respawnPoint, PlayerHealth health, PlayerCombat pistol,
                                WeaponManager weapons, ShotgunController shotgun, RifleController rifle,
                                EncounterTrigger[] encountersToReset = null, BossStateMachine bossToReset = null,
                                DeimosStateMachine deimosToReset = null, EncounterTrigger phobosToReset = null)
    {
        if (respawnPoint == null || health == null || pistol == null)
        {
            Debug.LogWarning("[CheckpointManager] SaveCheckpoint called with missing required references -- ignored.");
            return;
        }

        var snap = new Snapshot
        {
            position    = respawnPoint.position,
            yaw         = respawnPoint.eulerAngles.y,
            // Floored at 1 -- if health were somehow 0 here, Die() would already have
            // fired and TakeDamage() short-circuits further damage, so this is just a
            // defensive floor, not an expected path.
            health      = Mathf.Max(1f, health.CurrentHealth),
            medkitCount = health.MedkitCount,
            pistolMag   = pistol.CurrentMag,
            pistolSpare = pistol.SpareAmmo,
        };

        if (weapons != null && weapons.IsShotgunUnlocked && shotgun != null)
        {
            snap.shotgunUnlocked = true;
            snap.shotgunMag      = shotgun.CurrentMag;
            snap.shotgunSpare    = shotgun.SpareShells;
        }

        if (weapons != null && weapons.IsRifleUnlocked && rifle != null)
        {
            snap.rifleUnlocked = true;
            snap.rifleMag       = rifle.CurrentMag;
            snap.rifleSpare     = rifle.SpareRounds;
        }

        snap.encountersToReset = encountersToReset;
        snap.bossToReset       = bossToReset;
        snap.deimosToReset     = deimosToReset;
        snap.phobosToReset     = phobosToReset;

        _snap         = snap;
        HasCheckpoint = true;
        OnCheckpointSaved?.Invoke();

        Debug.Log($"[CheckpointManager] Checkpoint saved at {snap.position}. " +
                  $"HP {snap.health:F0}, medkits {snap.medkitCount}, pistol {snap.pistolMag}/{snap.pistolSpare}" +
                  (snap.shotgunUnlocked ? $", shotgun {snap.shotgunMag}/{snap.shotgunSpare}" : "") +
                  (snap.rifleUnlocked   ? $", rifle {snap.rifleMag}/{snap.rifleSpare}"       : "") + ".");
    }

    // ─── Respawn ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DeathScreenUI.RestartGame() / CombatHUD.RestartScene() when their
    /// Restart button is pressed AND HasCheckpoint is true (they fall back to the
    /// original full scene reload otherwise). Teleports the player to the checkpoint,
    /// restores health/medkits/ammo, and rolls back any linked encounters. Fires
    /// PlayerHealth.OnPlayerRespawned (via health.Revive() below), which those same
    /// UI scripts listen for to hide their death panel/overlay.
    /// </summary>
    public void RespawnPlayer()
    {
        var player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            Debug.LogWarning($"[CheckpointManager] No GameObject tagged '{playerTag}' found to respawn.");
            return;
        }

        var health   = player.GetComponent<PlayerHealth>();
        var movement = player.GetComponent<PlayerMovement>();
        var pistol   = player.GetComponent<PlayerCombat>();
        var weapons  = player.GetComponent<WeaponManager>();
        var shotgun  = player.GetComponent<ShotgunController>();
        var rifle    = player.GetComponent<RifleController>();

        // Teleport before reviving movement so the CharacterController toggle in
        // TeleportTo() happens while the player is still in the "dead" state (no input
        // is being read either way, but this keeps the ordering unsurprising).
        movement?.TeleportTo(_snap.position, _snap.yaw);

        health?.Revive(_snap.health, _snap.medkitCount);
        movement?.Revive();
        pistol?.Revive(_snap.pistolMag, _snap.pistolSpare);

        // Revive whichever weapons are unlocked *right now*, not just the ones unlocked when
        // this checkpoint was saved -- a weapon picked up after the checkpoint still needs its
        // _isDead/_isReloading flags cleared or it stays silently non-functional after respawn
        // (this was the "rifle sometimes doesn't work after respawn" bug). Use the snapshot's
        // saved ammo when the checkpoint itself has it; otherwise keep the weapon's own current
        // ammo -- there's nothing to roll back to for a weapon unlocked after the checkpoint.
        bool shotgunUnlockedNow = shotgun != null &&
            (weapons == null ? _snap.shotgunUnlocked : weapons.IsShotgunUnlocked);
        if (shotgunUnlockedNow)
        {
            if (_snap.shotgunUnlocked) shotgun.Revive(_snap.shotgunMag, _snap.shotgunSpare);
            else                       shotgun.Revive(shotgun.CurrentMag, shotgun.SpareShells);
        }

        bool rifleUnlockedNow = rifle != null &&
            (weapons == null ? _snap.rifleUnlocked : weapons.IsRifleUnlocked);
        if (rifleUnlockedNow)
        {
            if (_snap.rifleUnlocked) rifle.Revive(_snap.rifleMag, _snap.rifleSpare);
            else                     rifle.Revive(rifle.CurrentMag, rifle.SpareRounds);
        }

        // Roll back whichever encounter(s) this checkpoint was placed ahead of -- every enemy
        // in each one resets to spawn/full health and the trigger re-arms, so the player refights
        // the same encounter(s) instead of finding them already-dead or half-dead.
        if (_snap.encountersToReset != null)
        {
            foreach (var encounter in _snap.encountersToReset)
                encounter?.ResetEncounter();
        }

        // Boss doesn't go through EncounterTrigger at all (separate architecture -- see
        // BossStateMachine/BossHealth), so it gets its own explicit reset here. No-ops if the
        // boss has already died. See BossStateMachine.ResetForCheckpointRespawn() for exactly
        // what "reset" means for the boss (full HP, Phase 1, back to its arena spawn point).
        _snap.bossToReset?.ResetForCheckpointRespawn();

        // Deimos is architecturally the same situation as ARES -- its own separate
        // DeimosStateMachine/DeimosHealth, no EncounterTrigger involvement -- so it gets the
        // same explicit reset call. No-ops if Deimos has already died. See
        // DeimosStateMachine.ResetForCheckpointRespawn().
        _snap.deimosToReset?.ResetForCheckpointRespawn();

        // Phobos (the Hash Table Brute) is an ordinary EncounterTrigger-routed enemy, unlike
        // ARES/Deimos -- this is just the same ResetEncounter() call the generic
        // encountersToReset loop above already makes, exposed as its own named slot so it's
        // explicit and easy to confirm at a glance rather than buried in that array. Whoever
        // wired the CheckpointTrigger should have assigned Phobos's EncounterTrigger here OR in
        // encountersToReset, not both -- see CheckpointTrigger's phobosToReset tooltip.
        _snap.phobosToReset?.ResetEncounter();

        Debug.Log("[CheckpointManager] Player respawned at checkpoint.");
    }
}
