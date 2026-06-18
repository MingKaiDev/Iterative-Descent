using UnityEngine;

/// <summary>
/// Interactable shotgun pickup. Place this (with InteractableBase + InteractableRegistrar)
/// on the shotgun prop GameObject on the floor.
///
/// When the player presses E:
///   1. PlayerCombat (pistol) is cleanly disabled.
///   2. ShotgunController is equipped with the configured starting ammo.
///   3. This pickup prop destroys itself.
///
/// The shotgun model toggle (show shotgun, hide pistol) is handled inside
/// ShotgunController.OnEnable() -- no extra wiring needed here.
/// </summary>
public class ShotgunPickupProp : MonoBehaviour, IInteractable
{
    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Ammo on Pickup")]
    [Tooltip("Shells loaded into the magazine when the player first picks this up.")]
    public int startingMag   = 2;
    [Tooltip("Spare shells given to the player on pickup.")]
    public int startingSpare = 12;

    // ─── IInteractable ───────────────────────────────────────────────────────────

    public string InteractLabel => "Pick up Shotgun";

    public void Interact(GameObject interactor)
    {
        // Prefer WeaponManager if present -- it handles enable/disable and swap keys.
        WeaponManager wm = interactor.GetComponent<WeaponManager>();
        if (wm != null)
        {
            wm.UnlockShotgun(startingMag, startingSpare);
            Destroy(gameObject);
            return;
        }

        // Fallback: no WeaponManager -- equip directly (no swap key support).
        ShotgunController shotgun = interactor.GetComponent<ShotgunController>();
        if (shotgun == null)
        {
            Debug.LogWarning("[ShotgunPickupProp] Neither WeaponManager nor ShotgunController " +
                             "found on player.");
            return;
        }

        PlayerCombat pistol = interactor.GetComponent<PlayerCombat>();
        if (pistol != null)
        {
            pistol.CancelAim();
            pistol.enabled = false;
        }

        shotgun.EquipWithAmmo(startingMag, startingSpare);
        Debug.Log($"[ShotgunPickupProp] Shotgun equipped (no WeaponManager). Mag: {startingMag}, Spare: {startingSpare}.");

        Destroy(gameObject);
    }
}
