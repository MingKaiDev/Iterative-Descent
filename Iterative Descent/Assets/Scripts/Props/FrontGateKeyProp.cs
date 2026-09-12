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
//   1. Hides gateObject (SetActive(false)) -- same effect GateDisableProp had,
//      opening/removing the door/obstacle blocking the kitchen.
//   2. Destroys itself (Destroy(gameObject)) so the key actually despawns.
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
public class FrontGateKeyProp : MonoBehaviour, IInteractable
{
    [Header("Gate/Door To Open")]
    [Tooltip("The door/obstacle GameObject to hide (SetActive(false)) the moment this key is picked up.")]
    public GameObject gateObject;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractLabel => "Pick Up Front Gate Key";

    private bool _collected;

    public void Interact(GameObject interactor)
    {
        if (_collected) return;
        _collected = true;

        if (gateObject != null)
            gateObject.SetActive(false);
        else
            Debug.LogWarning("[KitchenKeyProp] No gateObject assigned.", this);

        Destroy(gameObject);
    }
}
