using UnityEngine;

/// <summary>
/// Attach alongside InteractableBase + InteractableRegistrar on the computer screen object.
/// Mirrors PuzzleProp pattern exactly.
/// </summary>
public class ComputerScreenProp : MonoBehaviour, IInteractable
{
    [Header("UI")]
    [Tooltip("Assign the PC Password Panel GameObject in the Canvas.")]
    public GameObject passwordOverlay;

    public string InteractLabel => "Use Computer";

    private bool _screenOpen;

    void Awake()
    {
        if (passwordOverlay != null) passwordOverlay.SetActive(false);
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
            print("Ayy");
            passwordOverlay.SetActive(true);
            passwordOverlay.GetComponent<PasswordScreenUI>().Setup(CloseScreen);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void CloseScreen()
    {
        _screenOpen = false;
        PlayerInteractor.Resume();

        if (passwordOverlay != null) passwordOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }
}