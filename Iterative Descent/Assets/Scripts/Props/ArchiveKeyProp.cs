// KitchenKeyProp.cs
// IInteractable for the Kitchen Key prop.
//
// Previously this GameObject reused GateDisableProp (built for the Hallway 2
// courtyard-gate control panel) to open the kitchen door. That script only
// disables its own three components after firing -- it never destroys the
// GameObject -- so the key prop visually stayed sitting in the world after
// being "picked up." This script gives the Kitchen Key its own dedicated
// pickup behaviour instead, mirroring MeetingRoomKeyProp/RiflePickupProp/
// PickupProp's "consumed on pickup" pattern.
//
// On pickup:
//   1. Unlocks the Archive Doors assigned to it in the inspector
//   2. Disables InteractableBase and InteractableRegistrar before disabling
//      itself.
//
// Setup:
//   1. On the Kitchen Key GameObject, remove the GateDisableProp component
//      and add this script instead. InteractableBase + InteractableRegistrar
//      stay as they are.
//   2. Re-assign 'gateObject' to whatever door/obstacle object
//      GateDisableProp previously pointed at (swapping the component does
//      NOT carry the old reference over -- it resets to None).
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class ArchiveKeyProp : MonoBehaviour, IInteractable
{
    [Header("Gate/Door To Open")]
    [Tooltip("The door/obstacle GameObject to hide (SetActive(false)) the moment this key is picked up.")]
    [SerializeField] private DoorController[] doors;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractLabel => "Open Archive Door";

    private bool _collected;

    public void Interact(GameObject interactor)
    {
        if (_collected) return;
        _collected = true;
        
        if (doors == null || doors.Length == 0)
        {
            Debug.LogWarning($"[ArchiveKeyProp] '{gameObject.name}' has no doors assigned -- nothing to unlock.", gameObject);
            return;
        }

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null)
            {
                Debug.LogWarning($"[ArchiveKeyProp] '{gameObject.name}' doors[{i}] is unassigned -- skipped.", gameObject);
                continue;
            }

            Debug.Log($"[ArchiveKeyProp] Fired on '{gameObject.name}' (scene: {gameObject.scene.name}) — unlocking '{doors[i].name}'.", gameObject);
            doors[i].Unlock();
        }
        GetComponent<InteractableBase>().enabled = false;
        GetComponent<InteractableRegistrar>().enabled = false;
        this.enabled = false;
    }
}
