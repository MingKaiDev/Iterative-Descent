using UnityEngine;

/// <summary>
/// Attach to a trigger collider anywhere you want a checkpoint. When the player walks
/// over it, snapshots their current health, medkits, and ammo via CheckpointManager --
/// if they die at any point after this, they respawn right here with that snapshot
/// restored instead of the level reloading.
///
/// Setup (same pattern as RoomTrigger):
///   1. Create a child GameObject with a Box Collider (Is Trigger = true) where you
///      want the checkpoint to be.
///   2. Attach this script. Give it a unique checkpointID -- shown in the Console log
///      only, doesn't need to match anything else.
///   3. Make sure the Player GameObject has the tag "Player" and carries PlayerHealth /
///      PlayerCombat / WeaponManager / ShotgunController / RifleController (the normal
///      Player prefab setup already does).
///   4. Optional: assign respawnPoint to a separate Transform if you want the player to
///      face a specific direction on respawn (e.g. into the room) rather than reusing
///      this trigger's own transform.
///
/// One-shot: after saving, this GameObject deactivates itself (SetActive(false), not
/// .enabled -- see the project's standing gotcha on trigger colliders and disabled
/// components) so it can never fire again. This matters for backtracking -- without it,
/// walking back through an earlier checkpoint after already reaching a later one would
/// silently regress the active checkpoint back to this earlier spot, undoing progress
/// the player doesn't expect to lose. A used checkpoint stays spent for the rest of the
/// playthrough; only reaching a fresh, not-yet-triggered CheckpointTrigger updates the
/// active checkpoint again.
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("Unique identifier for this checkpoint, for the Console log only.")]
    [SerializeField] private string checkpointID = "Checkpoint_Unnamed";

    [Tooltip("Tag on the Player GameObject.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Optional: exact transform (position + facing) the player respawns at. " +
             "Leave unassigned to use this trigger's own transform.")]
    [SerializeField] private Transform respawnPoint;

    [Header("Checkpoint Rollback")]
    [Tooltip("EncounterTrigger(s) that lie AHEAD of this checkpoint (not yet reached). If the " +
             "player dies anywhere after this checkpoint and respawns here, each one listed here " +
             "gets ResetEncounter() called on it -- re-arming the trigger and restoring every enemy " +
             "in it to spawn/full health, so the same fight happens again from scratch. E.g. the " +
             "checkpoint placed just before Encounter 2 should list Encounter 2's EncounterTrigger here.")]
    [SerializeField] private EncounterTrigger[] encountersToReset;

    [Header("Boss Checkpoint (optional)")]
    [Tooltip("Assign ONLY for a checkpoint placed inside/near the boss arena. If the player " +
             "dies mid-boss-fight and respawns here, the boss gets a full reset -- back to " +
             "full HP, Phase 1, and its original arena spawn position -- via " +
             "BossStateMachine.ResetForCheckpointRespawn(). Leave unassigned everywhere else; " +
             "no-ops safely if the boss has already died.")]
    [SerializeField] private BossStateMachine bossToReset;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        var health  = other.GetComponent<PlayerHealth>();
        var pistol  = other.GetComponent<PlayerCombat>();
        var weapons = other.GetComponent<WeaponManager>();
        var shotgun = other.GetComponent<ShotgunController>();
        var rifle   = other.GetComponent<RifleController>();

        if (health == null || pistol == null)
        {
            Debug.LogWarning($"[CheckpointTrigger] '{checkpointID}' -- player is missing " +
                              "PlayerHealth/PlayerCombat, cannot save checkpoint.");
            return;
        }

        CheckpointManager.Instance.SaveCheckpoint(
            respawnPoint != null ? respawnPoint : transform,
            health, pistol, weapons, shotgun, rifle, encountersToReset, bossToReset);

        Debug.Log($"[CheckpointTrigger] '{checkpointID}' reached and saved -- deactivating so " +
                  "backtracking through it later can't re-save/regress the checkpoint.");

        // One-shot -- see class doc comment. SetActive(false), not `enabled = false`: Unity
        // still delivers OnTriggerEnter to a disabled component's collider (physics messages
        // ignore MonoBehaviour.enabled), so only deactivating the whole GameObject reliably
        // stops this from firing again on a later backtrack through the same spot.
        gameObject.SetActive(false);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, $"Checkpoint: {checkpointID}");
#endif
    }
}
