using UnityEngine;

/// <summary>
/// IInteractable for the trophy-case padlock.
/// Opens the NumberLockUI overlay on interact.
/// correctCode is left empty for now (always-deny).
/// Wire a code here in a future sprint once the player has a way to find it.
/// </summary>
[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class PadlockProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("Lock UI")]
    [Tooltip("The root panel GameObject that contains the NumberLockUI component.")]
    [SerializeField] private GameObject   numberLockPanel;
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
        if (numberLockPanel == null || numberLockUI == null)
        {
            Debug.LogWarning("[PadlockProp] NumberLockPanel or NumberLockUI reference is missing.");
            return;
        }

        PlayerInteractor.Pause();

        numberLockUI.OnRequestClose -= HandleClose;
        numberLockUI.OnRequestClose += HandleClose;

        numberLockUI.OnUnlocked -= HandleUnlocked;
        numberLockUI.OnUnlocked += HandleUnlocked;

        numberLockPanel.SetActive(true);

        string code = string.IsNullOrEmpty(correctCode) ? null : correctCode;
        numberLockUI.Open(code);

        PlayerInteractor.RegisterCloseable(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;
    }

    // ICloseable
    public void Close() => CloseUI();

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

        // Disable this prop so the player cannot interact with it again
        enabled = false;
        var ib = GetComponent<InteractableBase>();
        if (ib != null) ib.enabled = false;
    }

    private void CloseUI()
    {
        numberLockUI.OnRequestClose -= HandleClose;
        numberLockUI.OnUnlocked     -= HandleUnlocked;

        numberLockPanel.SetActive(false);
        numberLockUI.Close();
        PlayerInteractor.DeregisterCloseable();
        PlayerInteractor.Resume();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}
