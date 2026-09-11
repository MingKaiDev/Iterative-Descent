// DialoguePickupProp.cs
// Generic IInteractable for any prop that should play a line of dialogue on
// interact -- e.g. a flavor pickup like the Kitchen's cola bottle commenting
// "All this for a bottle of cola." Generalized from the original one-off
// ColaProp.cs so the same script can be reused for future dialogue pickups
// (a note, a snack, an easter egg prop, etc.) instead of writing a new
// dedicated script per item -- just drop this on the prop and assign its own
// itemName + pickupDialogue per instance.
//
// Setup:
//   1. Add InteractableBase + InteractableRegistrar + this script to the prop GameObject.
//   2. Set 'itemName' -- shown on the interact prompt as "Pick Up {itemName}".
//   3. Assign 'pickupDialogue' to whatever DialogueSequence should play on interact
//      (e.g. Assets/Dialogue/Player/Player_ColaPickup.asset for the cola prop).
//      Leave empty to pick up silently.
//   4. Leave 'destroyOnPickup' checked (default) for a normal one-time pickup that
//      disappears from the world, matching every other consumable prop in this
//      project. Uncheck it for a prop that should stay in the world and remain
//      interactable (see 'playOnce' below for whether it can talk more than once).
//   5. 'playOnce' only matters when destroyOnPickup is unchecked -- checked
//      (default) means the dialogue fires the first time only and every later
//      interact is a silent no-op; unchecked means it fires every single time
//      the player interacts with it.
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class DialoguePickupProp : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [Tooltip("Shown on the interact prompt as 'Pick Up {itemName}'.")]
    public string itemName = "Item";

    [Tooltip("If true (default), this prop is consumed -- destroyed after the first interact, " +
             "same as every other pickup in this project. If false, it stays in the world and " +
             "can be interacted with again; see 'Play Once' below for repeat-dialogue behaviour.")]
    public bool destroyOnPickup = true;

    [Tooltip("Only relevant when Destroy On Pickup is false. If true (default), the dialogue " +
             "plays once and every later interact is a silent no-op. If false, the dialogue " +
             "plays again every time the player interacts with this prop.")]
    public bool playOnce = true;

    [Header("Dialogue (optional)")]
    [Tooltip("Line(s) played via DialogueManager.Enqueue() the instant this is interacted with. " +
             "Leave empty to skip and just pick it up (or trigger destroyOnPickup) silently.")]
    [SerializeField] private DialogueSequence pickupDialogue;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractLabel => $"Pick Up {itemName}";

    private bool _fired;

    public void Interact(GameObject interactor)
    {
        // Repeat guard only applies to props that stick around (destroyOnPickup
        // handles the one-shot case naturally -- the GameObject is simply gone).
        if (!destroyOnPickup && playOnce && _fired) return;
        _fired = true;

        if (pickupDialogue != null)
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Enqueue(pickupDialogue);
            else
                Debug.LogWarning($"[DialoguePickupProp] DialogueManager.Instance is null -- pickupDialogue will not play. ({itemName})", this);
        }

        if (destroyOnPickup)
            Destroy(gameObject);
    }
}
