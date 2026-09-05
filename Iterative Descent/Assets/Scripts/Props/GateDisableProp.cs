// GateDisableProp.cs
// IInteractable for the Hallway 2 control panel that hides the courtyard gate.
//
// Setup:
//   1. Add InteractableBase   (outline, prompt, radius)
//   2. Add InteractableRegistrar  (auto-registers into PlayerInteractor)
//   3. Add this script
//   4. Assign the gate GameObject (e.g. Door South) to gateObject in the Inspector
//
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class GateDisableProp : MonoBehaviour, IInteractable
{
    [Header("Gate")]
    [Tooltip("The gate GameObject to hide when the player activates this panel.")]
    public GameObject gateObject;

    [Tooltip("The label for the interaction.")]
    public string InteractLabel => "Open Front Gate";

    // ── IInteractable ─────────────────────────────────────────────────────────


    public void Interact(GameObject interactor)
    {
        if (gateObject != null)
            gateObject.SetActive(false);
        else
            Debug.LogWarning("[GateDisableProp] No gateObject assigned.", this);

        // Disable self so it cannot be triggered again.
        GetComponent<InteractableBase>().enabled = false;
        GetComponent<InteractableRegistrar>().enabled = false;
        this.enabled = false;
    }
}
