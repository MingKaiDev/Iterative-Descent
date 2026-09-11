// PersistentUIRoot.cs
// Canvas (and the EventSystem alongside it) is scene-local by default -- unlike the
// Player (PersistentPlayer), the DDA tracker (PlayerMetricsTracker), and the checkpoint
// manager, which are already DontDestroyOnLoad -- so without this, the whole HUD
// (CombatHUD, pause menu, death screen, selection prompt, puzzle panels, etc.)
// disappears the instant LevelTransitionManager loads a new scene, even though the
// player stats it displays are still intact underneath.
//
// Attach to EVERY top-level root GameObject the HUD needs to survive a level
// transition -- typically:
//   - the main Canvas (whatever GameObject parents CombatHUD/PauseMenuUI/DeathScreenUI/
//     SelectionPromptUI/the puzzle panels/etc.)
//   - the EventSystem GameObject, if it sits alongside Canvas as its own root object
//     (the Unity default) rather than nested under it -- without this surviving too,
//     button clicks/UI navigation in the new scene have nothing to route through.
//
// Give each persisted root a distinct 'id' (defaults to the GameObject's own name) --
// unlike PersistentPlayer (one Player, one static field), this script can end up on
// several different root objects at once, so the dedupe-on-reload guard below is keyed
// per id rather than a single shared static -- otherwise persisting the Canvas would
// end up destroying the EventSystem (or vice versa) the moment either one reloads.
//
// Setup:
//   1. Add this component to the Canvas root GameObject. Leave 'id' as default (uses
//      the GameObject's name, e.g. "Canvas") unless two persisted roots would otherwise
//      share the same name.
//   2. Add it to the EventSystem root GameObject too, if present as its own root object.
//   3. If DDADisplayHUD, BossHUD, LockedDoorHUD, or any other *HUD script lives on a
//      DIFFERENT root object than the main Canvas (not nested under it), add this here
//      too, or move it under the Canvas so one PersistentUIRoot covers it.
//   4. Do NOT place a second Canvas/EventSystem in Level 2 (or any future level) --
//      same reasoning as PersistentPlayer's Camera.main warning. Level 2 only needs its
//      own scene content (geometry, puzzle props, enemies, LevelSpawnPoint); any new
//      puzzle panels Level 2 introduces get added as new children under this same
//      persisted Canvas instead of a separate one.
using System.Collections.Generic;
using UnityEngine;

public class PersistentUIRoot : MonoBehaviour
{
    [Tooltip("Distinguishes this persisted root from any others (e.g. Canvas vs EventSystem) " +
             "so they don't dedupe against each other. Defaults to this GameObject's name.")]
    public string id;

    private static readonly Dictionary<string, PersistentUIRoot> _instances = new();

    void Awake()
    {
        if (string.IsNullOrEmpty(id)) id = gameObject.name;

        if (_instances.TryGetValue(id, out var existing) && existing != null && existing != this)
        {
            // A duplicate root woke up in a newly-loaded scene while the original
            // persisted one is still alive -- destroy the newcomer, keep the original.
            Destroy(gameObject);
            return;
        }

        _instances[id] = this;
        DontDestroyOnLoad(gameObject);
    }
}
