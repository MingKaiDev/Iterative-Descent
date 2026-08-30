using UnityEngine;

/// <summary>
/// Pickup that adds rifle rounds to the player's spare ammo pool.
/// Attach to the RifleRounds prefab alongside a trigger Collider.
///
/// Calls RifleController.AddAmmo() which broadcasts OnAmmoChanged automatically.
/// </summary>
public class RifleAmmoPickup : PickupBase
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Rounds")]
    [Tooltip("Rounds added to spare ammo pool on pickup.")]
    public int roundAmount = 5;

    // ─── PickupBase ───────────────────────────────────────────────────────────

    protected override void ApplyPickup(GameObject player)
    {
        RifleController rifle = player.GetComponent<RifleController>();
        if (rifle == null)
        {
            Debug.LogWarning("[RifleAmmoPickup] RifleController not found on player GameObject.");
            return;
        }

        rifle.AddAmmo(roundAmount);
        Debug.Log($"[RifleAmmoPickup] Collected. +{roundAmount} spare rounds.");
    }
}
