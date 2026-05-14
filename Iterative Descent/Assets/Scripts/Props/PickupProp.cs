// PickupProp.cs
using UnityEngine;
using UnityEngine.Events;

public class PickupProp : MonoBehaviour, IInteractable
{
    [Header("Item")]
    public string itemName = "Item";
    public bool destroyOnPickup = true;

    [Header("Events")]
    public UnityEvent<GameObject> onPickedUp;

    public string InteractLabel => $"Pick up {itemName}";

    public void Interact(GameObject interactor)
    {
        onPickedUp?.Invoke(interactor);
        Debug.Log($"[PickupProp] Picked up '{itemName}'.");
        if (destroyOnPickup) Destroy(gameObject);
    }
}