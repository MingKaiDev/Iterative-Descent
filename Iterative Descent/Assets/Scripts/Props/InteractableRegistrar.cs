// InteractableRegistrar.cs
// Attach alongside InteractableBase on every interactable prefab.
// Self-registers into PlayerInteractor's static list so
// FindObjectsByType is never called at runtime.
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
public class InteractableRegistrar : MonoBehaviour
{
    void OnEnable() => PlayerInteractor.Register(GetComponent<InteractableBase>());
    void OnDisable() => PlayerInteractor.Unregister(GetComponent<InteractableBase>());
}