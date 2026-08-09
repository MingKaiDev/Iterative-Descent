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

    [Header("Trophy Case (physical unlock feedback)")]
    [Tooltip("Door(s) on the trophy case that should swing open on unlock. Reuses the " +
             "existing DoorController -- it captures whatever rotation the mesh starts at " +
             "as \"closed\" and rotates by openAngle from there, so it does not matter that " +
             "the case geometry came out of Blender with an odd base rotation. Assign one " +
             "DoorController per door (e.g. two for a double door) and tune openAngle / " +
             "openPositionOffset per instance in the Inspector -- exact values need to be " +
             "eyeballed in Editor, not guessed blind.")]
    [SerializeField] private DoorController[] caseDoors;

    [Tooltip("The physical lock mesh/prop to hide once the padlock is solved. Separate from " +
             "unlockedObject above (that one is for the ammo reward, this one just disappears).")]
    [SerializeField] private GameObject lockVisual;

    /// <summary>Set true once a hint source (e.g. a readable note) has revealed this
    /// padlock's code. NumberLockUI shows the code on-screen while this is true.
    /// Static: there is currently only one padlock in the project. If a second one is
    /// ever added, this needs to become per-instance instead.</summary>
    public static bool CodeRevealed = false;

    /// <summary>Call from a hint source (e.g. PadlockCodeHintHandler) to reveal the code.</summary>
    public void RevealCode() => CodeRevealed = true;

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
        numberLockUI.Open(code, CodeRevealed);

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

        if (lockVisual != null)
            lockVisual.SetActive(false);

        if (caseDoors != null)
        {
            foreach (var door in caseDoors)
            {
                if (door == null) continue;
                door.isLocked = false; // these doors are gated by the padlock, not their own lock state
                door.Open();
            }
        }

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
