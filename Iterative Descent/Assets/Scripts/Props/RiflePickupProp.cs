using UnityEngine;

/// <summary>
/// Interactable rifle pickup. Place this (with InteractableBase + InteractableRegistrar)
/// on the rifle prop GameObject in the kitchen.
///
/// When the player presses E:
///   1. The currently active weapon (pistol/shotgun) is cleanly disabled.
///   2. RifleController is equipped with the configured starting ammo.
///   3. This pickup prop destroys itself.
///
/// The rifle model toggle (show rifle, hide previous weapon) is handled inside
/// RifleController.OnEnable() -- no extra wiring needed here.
/// </summary>
public class RiflePickupProp : MonoBehaviour, IInteractable
{
    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Ammo on Pickup")]
    [Tooltip("Rounds loaded into the magazine when the player first picks this up.")]
    public int startingMag   = 5;
    [Tooltip("Spare rounds given to the player on pickup.")]
    public int startingSpare = 10;

    // ─── IInteractable ───────────────────────────────────────────────────────────

    public string InteractLabel => "Pick up Rifle";

    public void Interact(GameObject interactor)
    {
        // Prefer WeaponManager if present -- it handles enable/disable and swap keys.
        WeaponManager wm = interactor.GetComponent<WeaponManager>();
        if (wm != null)
        {
            wm.UnlockRifle(startingMag, startingSpare);
            Destroy(gameObject);
            return;
        }

        // Fallback: no WeaponManager -- equip directly (no swap key support).
        RifleController rifle = interactor.GetComponent<RifleController>();
        if (rifle == null)
        {
            Debug.LogWarning("[RiflePickupProp] Neither WeaponManager nor RifleController " +
                             "found on player.");
            return;
        }

        PlayerCombat pistol = interactor.GetComponent<PlayerCombat>();
        if (pistol != null)
        {
            pistol.CancelAim();
            pistol.enabled = false;
        }

        ShotgunController shotgun = interactor.GetComponent<ShotgunController>();
        if (shotgun != null)
        {
            shotgun.CancelAim();
            shotgun.enabled = false;
        }

        rifle.EquipWithAmmo(startingMag, startingSpare);
        Debug.Log($"[RiflePickupProp] Rifle equipped (no WeaponManager). Mag: {startingMag}, Spare: {startingSpare}.");

        Destroy(gameObject);
    }
}
