using UnityEngine;

/// <summary>
/// Attach to the fist bone GameObject (the same object as the BoxCollider hitbox).
/// OnTriggerEnter can only fire on the GameObject that owns the Collider,
/// so this relay forwards hits up to EnemyAttack on the enemy root.
///
/// ─── Setup ────────────────────────────────────────────────────────────────
///   1. Select your fist bone in the Hierarchy.
///   2. Add Component → FistHitboxRelay.
///   3. Add Component → BoxCollider, enable "Is Trigger".
///   4. Size the box to roughly cover the fist area.
///   5. Leave parentAttack empty — auto-found from parents at Awake.
///      Or assign manually if the auto-find is too slow.
///   6. Assign the BoxCollider to EnemyAttack.fistHitbox in the Inspector.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FistHitboxRelay : MonoBehaviour
{
    [Tooltip("Leave empty — auto-found in parent. Assign manually if needed.")]
    [SerializeField] private EnemyAttack parentAttack;

    private void Awake()
    {
        if (parentAttack == null)
            parentAttack = GetComponentInParent<EnemyAttack>();

        if (parentAttack == null)
            Debug.LogError($"[FistHitboxRelay] No EnemyAttack found in parents of '{gameObject.name}'. " +
                           "Assign parentAttack manually in the Inspector.");
    }

    private void OnTriggerEnter(Collider other)
    {
        parentAttack?.OnFistHit(other);
    }
}
