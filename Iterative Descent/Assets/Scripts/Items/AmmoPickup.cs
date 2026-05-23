using UnityEngine;

/// <summary>
/// Pickup that restores bullets to the player's spare ammo pool.
/// Attach to the AmmoBox prefab alongside a trigger Collider.
///
/// Calls PlayerCombat.AddAmmo() which handles HUD broadcast automatically.
/// </summary>
public class AmmoPickup : PickupBase
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Ammo")]
    [Tooltip("Bullets added to spare ammo pool on pickup.")]
    public int ammoAmount = 6;

    // ─── PickupBase ───────────────────────────────────────────────────────────

    protected override void ApplyPickup(GameObject player)
    {
        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        if (combat == null)
        {
            Debug.LogWarning("[AmmoPickup] PlayerCombat not found on player GameObject.");
            return;
        }

        combat.AddAmmo(ammoAmount);
        Debug.Log($"[AmmoPickup] Collected. +{ammoAmount} spare ammo.");
    }
}
