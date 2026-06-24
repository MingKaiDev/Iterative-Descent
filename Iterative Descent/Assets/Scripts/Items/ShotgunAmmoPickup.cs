using UnityEngine;

/// <summary>
/// Pickup that adds shotgun shells to the player's spare shell pool.
/// Attach to the ShotgunShells prefab alongside a trigger Collider.
///
/// Calls ShotgunController.AddAmmo() which broadcasts OnAmmoChanged automatically.
/// </summary>
public class ShotgunAmmoPickup : PickupBase
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Shells")]
    [Tooltip("Shells added to spare shell pool on pickup.")]
    public int shellAmount = 4;

    // ─── PickupBase ───────────────────────────────────────────────────────────

    protected override void ApplyPickup(GameObject player)
    {
        ShotgunController shotgun = player.GetComponent<ShotgunController>();
        if (shotgun == null)
        {
            Debug.LogWarning("[ShotgunAmmoPickup] ShotgunController not found on player GameObject.");
            return;
        }

        shotgun.AddAmmo(shellAmount);
        Debug.Log($"[ShotgunAmmoPickup] Collected. +{shellAmount} spare shells.");
    }
}
