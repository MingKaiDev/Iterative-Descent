// ColaProp.cs
// IInteractable for the cola bottle prop in the Kitchen -- a flavor pickup
// with no gameplay effect. Plays a short player one-liner on pickup, then
// destroys itself, mirroring KitchenKeyProp/MeetingRoomKeyProp/RiflePickupProp's
// "consumed on pickup" pattern.
//
// Setup:
//   1. Add InteractableBase + InteractableRegistrar + this script to the cola prop GameObject.
//   2. Assign 'pickupDialogue' to Assets/Dialogue/Player/Player_ColaPickup.asset
//      ("All this for a bottle of cola."), or leave empty to pick it up silently.
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class ColaProp : MonoBehaviour, IInteractable
{
    [Header("Dialogue (optional)")]
    [Tooltip("Player one-liner played via DialogueManager.Enqueue() the instant this is picked up -- " +
             "e.g. 'All this for a bottle of cola.' See Assets/Dialogue/Player/Player_ColaPickup.asset. " +
             "Leave empty to pick up silently.")]
    [SerializeField] private DialogueSequence pickupDialogue;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractLabel => "Pick Up Cola";

    private bool _collected;

    public void Interact(GameObject interactor)
    {
        if (_collected) return;
        _collected = true;

        if (pickupDialogue != null)
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Enqueue(pickupDialogue);
            else
                Debug.LogWarning("[ColaProp] DialogueManager.Instance is null -- pickupDialogue will not play.", this);
        }

        Destroy(gameObject);
    }
}
