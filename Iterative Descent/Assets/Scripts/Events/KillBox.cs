using UnityEngine;

/// <summary>
/// Safety-net trigger volume placed well below the level (e.g. under the floor of every
/// room, at a Y low enough that nothing legitimate ever reaches it). If the player somehow
/// clips through geometry and falls out of the map, this instantly kills them so the
/// existing death/checkpoint flow (PlayerHealth.Die -> OnPlayerDied -> DeathScreenUI /
/// CombatHUD -> CheckpointManager.RespawnPlayer) takes over, instead of leaving them
/// falling forever.
///
/// Setup:
///   1. Create an empty GameObject well below the lowest floor (e.g. Y = -50).
///   2. Add a large Box Collider covering the full level footprint on the X/Z plane,
///      with Is Trigger = true.
///   3. Attach this script.
///   4. Make sure the Player GameObject (or a parent of its colliders) has the tag "Player".
/// </summary>
public class KillBox : MonoBehaviour
{
    [Tooltip("Tag on the Player GameObject.")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            Debug.LogWarning("[KillBox] Player entered kill volume but no IDamageable found on it.");
            return;
        }

        // Infinite damage guarantees death regardless of current health.
        damageable.TakeDamage(Mathf.Infinity, transform.position);
        Debug.Log("[KillBox] Player fell out of the map, instant-killed.");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, "Kill Box");
#endif
    }
}
