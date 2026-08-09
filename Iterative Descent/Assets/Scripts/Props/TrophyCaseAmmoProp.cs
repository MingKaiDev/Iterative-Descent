// TrophyCaseAmmoProp.cs
// IInteractable ammo reward inside the trophy case, granted on E-press rather
// than by walking over a physical pickup (see PickupBase for the walk-over
// pattern used elsewhere -- this is a deliberate departure for this one prop).
//
// SETUP:
//   1. Place on a GameObject inside/near the trophy case (a locker shelf, a
//      box, whatever reads as "the ammo you can now reach"). Start it INACTIVE
//      in the scene.
//   2. Add InteractableBase + InteractableRegistrar (auto-required below).
//   3. Assign this GameObject to PadlockProp's existing "Unlocked Object"
//      field -- PadlockProp already calls SetActive(true) on that field when
//      the padlock is solved, so no extra wiring is needed to gate this behind
//      the lock. Nothing here needs to know about PadlockProp directly.
//
// One-shot: grants ammo once, then disables itself and its InteractableBase
// so the prompt/outline stop appearing and it cannot be triggered again.
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class TrophyCaseAmmoProp : MonoBehaviour, IInteractable
{
    [Header("Ammo")]
    [Tooltip("Bullets added to spare ammo pool on interact. Matches AmmoPickup's default.")]
    [SerializeField] private int ammoAmount = 6;

    [Tooltip("Label shown on the interact prompt.")]
    [SerializeField] private string interactLabel = "Take Ammo";

    public string InteractLabel => interactLabel;

    public void Interact(GameObject interactor)
    {
        PlayerCombat combat = interactor.GetComponent<PlayerCombat>();
        if (combat == null)
        {
            Debug.LogWarning("[TrophyCaseAmmoProp] PlayerCombat not found on interactor.");
            return;
        }

        combat.AddAmmo(ammoAmount);
        Debug.Log($"[TrophyCaseAmmoProp] Collected. +{ammoAmount} spare ammo.");

        // One-shot: disable so it cannot be interacted with again.
        var ib = GetComponent<InteractableBase>();
        if (ib != null) ib.enabled = false;
        var ir = GetComponent<InteractableRegistrar>();
        if (ir != null) ir.enabled = false;
        enabled = false;
    }
}
