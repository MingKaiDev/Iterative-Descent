using UnityEngine;

/// <summary>
/// Attach alongside InteractableBase + InteractableRegistrar on the computer screen object.
/// Mirrors PuzzleProp pattern exactly.
/// </summary>
public class ComputerScreenProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI")]
    [Tooltip("Assign the PC Password Panel GameObject in the Canvas.")]
    public GameObject passwordOverlay;

    [Tooltip("Assign the Linked List Puzzle Panel GameObject in the Canvas.")]
    public GameObject linkedListOverlay;

    public string InteractLabel => "Use Computer";

    private bool _screenOpen;

    void Awake()
    {
        if (passwordOverlay != null) passwordOverlay.SetActive(false);
        if (linkedListOverlay != null) linkedListOverlay.SetActive(false);
    }

    public void Interact(GameObject interactor)
    {
        if (_screenOpen) return;
        OpenScreen();
    }

    public void OpenScreen()
    {
        _screenOpen = true;
        PlayerInteractor.Pause();

        var interactBase = GetComponent<InteractableBase>();
        if (interactBase != null && interactBase.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        if (passwordOverlay != null)
        {
            passwordOverlay.SetActive(true);
            passwordOverlay.GetComponent<PasswordScreenUI>().Setup(CloseScreen);
        }

        PlayerInteractor.RegisterCloseable(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Called by PasswordEventHandler after OnPC1LoggedIn fires.
    /// Swaps the password panel out for the linked list puzzle panel.
    /// </summary>
    public void ShowLinkedListPuzzle()
    {
        if (passwordOverlay != null) passwordOverlay.SetActive(false);

        if (linkedListOverlay != null)
        {
            linkedListOverlay.SetActive(true);
            linkedListOverlay.GetComponent<LinkedListPuzzleUI>().InitPuzzle(CloseScreen);
        }
    }

    // ICloseable
    public void Close() => CloseScreen();

    public void CloseScreen()
    {
        _screenOpen = false;
        PlayerInteractor.DeregisterCloseable();
        PlayerInteractor.Resume();

        if (passwordOverlay != null) passwordOverlay.SetActive(false);
        if (linkedListOverlay != null) linkedListOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }
}