using UnityEngine;

/// <summary>
/// Pickup that adds a medkit to the player's inventory.
/// Attach to the HealthKit prefab alongside a trigger Collider.
///
/// 2026-09 revamp: no longer heals on contact. Calls PlayerHealth.AddMedkit(),
/// which the player later consumes with Q (see PlayerHealth.UseMedkit()) for a
/// flat medkitHealFraction of max health. If the player's medkit inventory is
/// already full, AddMedkit() returns false and this pickup declines itself --
/// it stays in the world (see PickupBase) rather than being wasted.
/// </summary>
public class HealthPickup : PickupBase
{
    // ─── PickupBase ───────────────────────────────────────────────────────────

    protected override bool ApplyPickup(GameObject player)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null)
        {
            Debug.LogWarning("[HealthPickup] PlayerHealth not found on player GameObject.");
            return true; // nothing we can do with it either way -- consume so it doesn't linger
        }

        bool added = health.AddMedkit();
        if (added)
            Debug.Log("[HealthPickup] Collected. Medkit added to inventory.");
        else
            Debug.Log("[HealthPickup] Medkit inventory full -- pickup left in world.");

        return added;
    }
}
