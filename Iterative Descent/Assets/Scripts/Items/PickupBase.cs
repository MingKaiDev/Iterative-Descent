using UnityEngine;

/// <summary>
/// Abstract base for all world pickups (ammo, health, etc.)
///
/// Setup in Unity:
///   1. Attach this (or a subclass) to a GameObject with the FBX mesh.
///   2. Add a BoxCollider (or SphereCollider), tick "Is Trigger".
///   3. Player GameObject must have tag "Player".
///
/// Subclasses override ApplyPickup() to define the effect and return whether
/// the item was actually consumed. Almost always true -- HealthPickup is the
/// one exception, returning false (declining the pickup, leaving it in the
/// world) when the player's medkit inventory is already at its cap.
/// The item bobs and rotates in-world to signal interactivity, and destroys
/// itself on collection. Double-pickup is guarded.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class PickupBase : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Bob and Rotate")]
    [SerializeField] private bool  enableBob   = true;
    [SerializeField] private float bobHeight   = 0.08f;
    [SerializeField] private float bobSpeed    = 2.2f;
    [SerializeField] private float rotateSpeed = 55f;

    // ─── Private ──────────────────────────────────────────────────────────────

    private Vector3 _originPos;
    private bool    _consumed;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    protected virtual void Start()
    {
        _originPos = transform.position;

        // Warn if the collider is not a trigger — pickup will never fire otherwise.
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
            Debug.LogWarning($"[PickupBase] Collider on '{name}' is not set to Is Trigger. " +
                             "The pickup will not work until you tick Is Trigger in the Inspector.");
    }

    protected virtual void Update()
    {
        if (!enableBob) return;
        float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = _originPos + new Vector3(0f, yOffset, 0f);
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    // ─── Trigger ──────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;
        if (!other.CompareTag("Player")) return;

        bool consumed = ApplyPickup(other.gameObject);
        if (!consumed) return; // e.g. HealthPickup declining a full medkit inventory

        _consumed = true;
        Destroy(gameObject);
    }

    // ─── Abstract ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called exactly once when the player touches this pickup (re-called on a
    /// later touch if a previous call returned false and the pickup is still here).
    /// Use <paramref name="player"/> to get PlayerHealth, PlayerCombat, etc.
    /// Return true if the item was actually consumed (destroy it), false to leave
    /// it in the world untouched.
    /// </summary>
    protected abstract bool ApplyPickup(GameObject player);
}
