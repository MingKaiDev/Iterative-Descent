using UnityEngine;

/// <summary>
/// Abstract base for all world pickups (ammo, health, etc.)
///
/// Setup in Unity:
///   1. Attach this (or a subclass) to a GameObject with the FBX mesh.
///   2. Add a BoxCollider (or SphereCollider), tick "Is Trigger".
///   3. Player GameObject must have tag "Player".
///
/// Subclasses override ApplyPickup() to define the effect.
/// The item bobs and rotates in-world to signal interactivity,
/// and destroys itself on collection. Double-pickup is guarded.
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

        _consumed = true;
        ApplyPickup(other.gameObject);
        Destroy(gameObject);
    }

    // ─── Abstract ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called exactly once when the player touches this pickup.
    /// Use <paramref name="player"/> to get PlayerHealth, PlayerCombat, etc.
    /// </summary>
    protected abstract void ApplyPickup(GameObject player);
}
