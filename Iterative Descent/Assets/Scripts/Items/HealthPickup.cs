using UnityEngine;

/// <summary>
/// Pickup that restores player health.
/// Attach to the HealthKit prefab alongside a trigger Collider.
///
/// Calls PlayerHealth.Heal() which handles HUD broadcast automatically.
/// Healing is clamped to maxHealth inside PlayerHealth — no overflow possible.
/// </summary>
public class HealthPickup : PickupBase
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Healing")]
    [Tooltip("HP restored on pickup. Clamped to player maxHealth internally.")]
    public float healAmount = 20f;

    // ─── PickupBase ───────────────────────────────────────────────────────────

    protected override void ApplyPickup(GameObject player)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null)
        {
            Debug.LogWarning("[HealthPickup] PlayerHealth not found on player GameObject.");
            return;
        }

        health.Heal(healAmount);
        Debug.Log($"[HealthPickup] Collected. +{healAmount} HP.");
    }
}
