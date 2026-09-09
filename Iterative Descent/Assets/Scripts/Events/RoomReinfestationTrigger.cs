using UnityEngine;

/// <summary>
/// Attach to a trigger collider at a room's entrance -- can live on the same
/// GameObject as that room's existing RoomTrigger (both scripts receive
/// OnTriggerEnter/OnTriggerExit independently and don't interfere with each
/// other).
///
/// Built for the General Office / Jason quest: the room must be clear the
/// FIRST time the player walks in (so the Jason setup dialogue plays without
/// combat interrupting it), but should be "infested" if the player leaves
/// and comes back later. Reusable for any other room that wants the same
/// "safe on first visit, hot on reentry" beat.
///
/// How it works: does nothing on the player's first OnTriggerEnter. The
/// moment the player EXITS the room for the first time, this activates
/// every assigned EncounterTrigger's GameObject. Only arms once; safe if
/// OnTriggerExit fires more than once for any reason.
///
/// ── Scene setup ──────────────────────────────────────────────────────────
///   1. Add this component to the room's entrance trigger (the same
///      GameObject as the room's existing RoomTrigger works fine).
///   2. Assign every EncounterTrigger that should stay dormant on the first
///      visit and go live on reentry to encountersToArm. Leave each of
///      those EncounterTrigger's GAMEOBJECTS -- not just the component's
///      own enabled tickbox -- UNCHECKED (inactive) in the Inspector by
///      default. See below for why this distinction matters.
///
/// ── Important: GameObject, not component enabled ─────────────────────────
///   Unity still delivers OnTriggerEnter to a disabled script's collider --
///   physics messages ignore MonoBehaviour.enabled, unlike Update or
///   OnEnable/OnDisable. So disabling just the EncounterTrigger component
///   does NOT reliably keep it inert; the encounter would still fire on
///   first entry. Deactivating the whole GameObject does reliably keep it
///   inert, because an inactive GameObject's colliders don't participate in
///   physics at all. This script arms by calling GameObject.SetActive(true)
///   on each assigned EncounterTrigger for exactly that reason.
///
/// ── Important: re-entry edge case ──────────────────────────────────────────
///   Activating a GameObject does NOT retroactively fire OnTriggerEnter for
///   a player already standing inside its collider -- Unity only calls
///   trigger callbacks on the frame a collider starts overlapping. Since
///   this script arms on EXIT (the player is, by definition, leaving the
///   room and no longer overlapping anything inside it), the assigned
///   EncounterTrigger colliders will correctly fire fresh the next time the
///   player walks back in. Just make sure each EncounterTrigger's own
///   collider sits somewhere the player must walk through to reach the
///   room, not somewhere they could already be standing when this arms.
/// </summary>
public class RoomReinfestationTrigger : MonoBehaviour
{
    [Header("Encounters (dormant on first visit, armed on exit)")]
    [Tooltip("EncounterTrigger components whose GameObjects get activated the moment the player first " +
             "leaves this room. Leave each of these GameObjects INACTIVE in the Inspector by default -- " +
             "not just the component's enabled tickbox. See the class doc comment for why that " +
             "distinction matters.")]
    [SerializeField] private EncounterTrigger[] encountersToArm;

    [Header("Trigger")]
    [Tooltip("Tag used to identify the player collider entering/exiting this trigger.")]
    [SerializeField] private string playerTag = "Player";

    private bool _armed;

    private void OnTriggerExit(Collider other)
    {
        if (_armed) return;
        if (!other.CompareTag(playerTag)) return;

        _armed = true;
        ArmEncounters();
    }

    private void ArmEncounters()
    {
        if (encountersToArm == null || encountersToArm.Length == 0)
        {
            Debug.LogWarning("[RoomReinfestationTrigger] No encounters assigned -- reentry will stay empty.", this);
            return;
        }

        int armed = 0;
        for (int i = 0; i < encountersToArm.Length; i++)
        {
            if (encountersToArm[i] == null)
            {
                Debug.LogWarning($"[RoomReinfestationTrigger] encountersToArm[{i}] is null -- skipping.", this);
                continue;
            }

            // SetActive(), not .enabled -- see the class doc comment. Toggling .enabled alone would not
            // actually keep the encounter inert against physics-driven OnTriggerEnter.
            encountersToArm[i].gameObject.SetActive(true);
            armed++;
        }

        Debug.Log($"[RoomReinfestationTrigger] Room armed for reentry. {armed}/{encountersToArm.Length} encounter(s) activated.");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0f, 0.8f, 0.2f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            $"RoomReinfestationTrigger ({(encountersToArm != null ? encountersToArm.Length : 0)} encounters, armed={_armed})");
#endif
    }
}
