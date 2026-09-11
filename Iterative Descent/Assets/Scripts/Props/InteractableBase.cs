using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableBase : MonoBehaviour
{
    [Header("Detection")]
    public float highlightRadius = 3f;
    public Transform propCenter;

    [Header("Prompt UI")]
    public GameObject promptPanel;
    public TMP_Text promptLabel;

    // ���� State set by PlayerInteractor each frame ����������������������������������
    [HideInInspector] public bool playerInRadius;
    [HideInInspector] public bool isSelected;       // highlighted via arrow key selection

    // ���� Cached ������������������������������������������������������������������������������������������������������
    private Outline _outline;
    private IInteractable _handler;

    public Vector3 Center => propCenter != null ? propCenter.position : transform.position;
    public IInteractable Handler => _handler;

    void Awake()
    {
        _outline = GetComponent<Outline>();
        _handler = GetComponent<IInteractable>();

        if (_outline != null) _outline.enabled = false;
        if (promptPanel != null) promptPanel.SetActive(false);

        if (_handler == null)
            Debug.LogWarning($"[InteractableBase] '{gameObject.name}' has no IInteractable component.", this);
    }

    /// <summary>
    /// Runs whenever this component is disabled -- including the
    /// self-disable pattern props like GateDisableProp use after firing once
    /// (enabled = false so it can't be triggered again). Without this the
    /// Outline component (a separate component, unaffected by disabling
    /// InteractableBase) stays in whatever state UpdateState() last left it,
    /// so the highlight kept showing permanently after interaction.
    /// </summary>
    void OnDisable()
    {
        if (_outline != null) _outline.enabled = false;
        if (promptPanel != null) promptPanel.SetActive(false);
    }

    /// <summary>Called every frame by PlayerInteractor.</summary>
    public void UpdateState(bool inRadius, bool selected)
    {
        playerInRadius = inRadius;
        isSelected = selected;

        // Outline: on when in range; brighter/different color when selected
        if (_outline != null)
        {
            _outline.enabled = inRadius;
            if (inRadius)
                _outline.OutlineColor = Color.white;
        }

        // Prompt: only for the currently selected object.
        //
        // promptPanel/promptLabel are left unassigned (fileID 0) on any prop that
        // wasn't authored in the same scene as the shared Canvas prompt panel -- e.g.
        // every prop inside the Library room prefab, edited in isolated Prefab Mode
        // (confirmed via direct prefab-file audit: BstPuzzleProp's InteractableBase
        // has both fields at fileID 0). Falling back to PromptPanelUI.Instance means
        // those props still show the "[E]  ..." prompt without needing a reference
        // that has nothing in the open scene to drag it from. See PromptPanelUI.cs's
        // own doc comment for the full explanation -- same shape of fix as
        // BstPuzzleUI.Instance / SelectionPromptUI.Instance elsewhere in this project.
        GameObject panel = promptPanel != null ? promptPanel : PromptPanelUI.Instance?.Panel;
        TMP_Text   label = promptLabel != null ? promptLabel : PromptPanelUI.Instance?.Label;

        if (panel != null)
        {
            panel.SetActive(inRadius);
            if (inRadius && label != null)
                label.text = $"[E]  {_handler?.InteractLabel ?? "Interact"}";
        }
    }

    public void TryInteract(GameObject interactor)
    {
        _handler?.Interact(interactor);
    }

    /// <summary>
    /// Force-hides this prop's "[E] ..." prompt immediately, resolving the same
    /// promptPanel-or-PromptPanelUI.Instance fallback UpdateState() uses. Every puzzle
    /// prop calls something like this from its own OpenPuzzle() right after pausing --
    /// PlayerInteractor.Update() returns early while paused, so UpdateState() never
    /// runs again to naturally drive the panel back to inactive, and the panel would
    /// otherwise stay stuck showing (bled through on top of the puzzle UI) at whatever
    /// state it was last in the instant before the pause.
    ///
    /// A prop that hides its promptPanel field directly (the old pattern, still fine
    /// for any prop with that field actually assigned) has always worked. This method
    /// exists so a prop relying on the PromptPanelUI fallback (promptPanel left null,
    /// e.g. BstPuzzleProp) hides the SAME shared panel it was actually showing --
    /// otherwise touching a null promptPanel field directly silently does nothing, and
    /// the shared prompt bar is left visible over the puzzle overlay. Confirmed via
    /// screenshot 2026-09-10: opening the BST puzzle left "[E]  Sort the Returns Cart"
    /// showing on top of the panel.
    /// </summary>
    public void HidePrompt()
    {
        GameObject panel = promptPanel != null ? promptPanel : PromptPanelUI.Instance?.Panel;
        if (panel != null) panel.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Center, highlightRadius);
    }
}