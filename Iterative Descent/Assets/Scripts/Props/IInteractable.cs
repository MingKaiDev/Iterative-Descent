// Interface for Interactable
using UnityEngine;

public interface IInteractable
{
    /// <summary>Label shown in the "Press E to interact"</summary>
    string InteractLabel { get; }

    /// <summary>Called by PlayerInteractor when the player successfully interacts.</summary>
    void Interact(GameObject interactor);
}
