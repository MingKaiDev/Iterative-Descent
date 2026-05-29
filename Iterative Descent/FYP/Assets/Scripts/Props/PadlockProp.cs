using UnityEngine;

/// <summary>
/// IInteractable for the trophy-case padlock.
/// Opens the NumberLockUI overlay on interact.
/// correctCode is left empty for now (always-deny).
/// Wire a code here in a future sprint once the player has a way to find it.
/// </summary>
[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class PadlockProp : MonoBehaviour, IInteractable
{
    [Header("Lock UI")]
    [SerializeField] private NumberLockUI numberLockUI;

    [Header("Unlock Config")]
    [Tooltip("Leave empty to always deny. Set to a 4-digit string (e.g. 1234) when the code is introduced.")]
    [SerializeField] private string correctCode = "";

    [Header("Optional: object to enable on unlock")]
    [SerializeField] private GameObject unlockedObject;

    // IInteractable
    public string InteractLabel => "Examine Padlock";

    public void Interact(GameObject interactor)
    {
        if (numberLockUI == null)
        {
            Debug.LogWarning("[PadlockProp] NumberLockUI reference is missing.");
            return;
        }

        PlayerInteractor.Instance?.Pause();

        numberLockUI.OnRequestClose -= HandleClose;
        numberLockUI.OnRequestClose += HandleClose;

        numberLockUI.OnUnlocked -= HandleUnlocked;
        numberLockUI.OnUnlocked += HandleUnlocked;

        string code = string.IsNullOrEmpty(correctCode) ? null : correctCode;
        numberLockUI.Open(code);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;
    }

    // ------------------------------------------------------------------

    private void HandleClose()
    {
        CloseUI();
    }

    private void HandleUnlocked()
    {
        CloseUI();

        if (unlockedObject != null)
            unlockedObject.SetActive(true);

        // Disable this prop so the player cannot interact with an open lock
        GetComponent<InteractableBase>()?.SetInteractable(false);
    }

    private void CloseUI()
    {
        numberLockUI.OnRequestClose -= HandleClose;
        numberLockUI.OnUnlocked     -= HandleUnlocked;

        numberLockUI.Close();
        PlayerInteractor.Instance?.Resume();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}
