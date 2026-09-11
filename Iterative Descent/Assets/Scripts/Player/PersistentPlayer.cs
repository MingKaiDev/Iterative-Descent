// PersistentPlayer.cs
// Attach to the root of the Player prefab/GameObject (the same object carrying
// CharacterController, PlayerMovement, PlayerHealth, PlayerCombat, WeaponManager,
// PlayerInteractor, and the FPS camera as a child).
//
// Makes the Player survive a LevelTransitionManager scene load instead of being
// destroyed and recreated from scratch in the new scene. This is the mechanism
// "ammo/health/unlocked weapons carry over to Level 2" actually relies on --
// because it's the literal same component instances, every stat (current health,
// medkit count, mag/spare ammo per weapon, which weapons are unlocked, which
// weapon is currently equipped, the Reagent Routing barrel attachment/settle-time
// upgrades, etc.) is intact automatically, with nothing to manually snapshot and
// restore -- same idea as CheckpointManager/PlayerMetricsTracker's own
// DontDestroyOnLoad singletons, just applied to the Player itself.
//
// Setup:
//   1. Add this component to the Player's root GameObject (Level 1 only -- do NOT
//      place a second Player prefab instance in Level 2's scene. LevelTransitionManager
//      teleports this same persisted Player into Level 2 via a LevelSpawnPoint marker
//      instead -- see LevelSpawnPoint.cs).
//   2. Nothing else to configure. The singleton-guard below only matters if a scene
//      is ever re-entered in a way that could spawn a second Player (e.g. a future
//      "return to Level 1" transition) -- it destroys the newcomer and keeps the
//      original, already-in-progress Player instead.
//
// Heads-up for when Level 2 is actually built: do NOT give Level 2 its own baked-in
// Player/Camera prefab instance. Camera.main resolves to the first active
// MainCamera-tagged object it finds -- a second camera in the new scene would make
// that ambiguous, on top of duplicating PlayerInteractor/PlayerMovement/etc. Only
// place a LevelSpawnPoint marker there; this persisted Player is teleported to it.
using UnityEngine;

public class PersistentPlayer : MonoBehaviour
{
    private static PersistentPlayer _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // A second Player instance woke up in a newly-loaded scene while the
            // original persisted Player is still alive -- destroy the newcomer
            // rather than ending up with two Players (and two Cameras, two
            // PlayerInteractors, etc.) both active at once.
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
