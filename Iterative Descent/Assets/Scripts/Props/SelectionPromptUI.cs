// SelectionPromptUI.cs
// Attach to a Screen Space Canvas in the scene (one instance only).
// Shows "< >  Cycling X objects" hint whenever multiple interactables are in range.
// Wire up in the Inspector: assign the hint panel + its TMP_Text label.
// Text is ASCII-only by project convention (no Unicode arrows/dashes -- TMP font
// compatibility, see CLAUDE.md and Story 28 notes).
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SelectionPromptUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("A small panel that shows the arrow-key cycling hint.")]
    public GameObject cycleHintPanel;

    [Tooltip("TMP label inside the hint panel.")]
    public TMP_Text cycleHintLabel;

    // ── Static registry mirrors PlayerInteractor's _inRange ──────
    // PlayerInteractor calls UpdateHint() each frame via singleton.
    //
    // This panel starts INACTIVE in the hierarchy by default (like every other puzzle/
    // HUD overlay), and Unity never runs a GameObject's Awake() while it's inactive --
    // only once something first calls SetActive(true) on it. Left as a plain auto-
    // property, that meant Instance could stay null indefinitely (Awake() simply never
    // firing), silently breaking the "< > 1/2 -- ..." cycle hint with no error anywhere
    // -- PlayerInteractor only ever reads Instance through a null-safe `?.`/`!= null`
    // check, so a null Instance never even logs a warning, it just quietly never shows
    // the hint. FindFirstObjectByType(..., FindObjectsInactive.Include) can locate the
    // component on an inactive GameObject without needing Awake() to have run first, so
    // it's used here as a one-time fallback; once found, the result is cached in
    // _instance so every later read is the cheap direct-field path again. Same fix as
    // BstPuzzleProp.ResolvePuzzleUI() applies to BstPuzzleUI.Instance, for the same reason.
    private static SelectionPromptUI _instance;
    public static SelectionPromptUI Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<SelectionPromptUI>(FindObjectsInactive.Include);
            return _instance;
        }
        private set => _instance = value;
    }

    void Awake()
    {
        Instance = this;
        if (cycleHintPanel != null) cycleHintPanel.SetActive(false);
    }

    /// <summary>
    /// Force-hides the cycle hint panel immediately. Needed because PlayerInteractor.Update()
    /// returns early while paused and never calls UpdateHint() again -- without this, the panel
    /// stays stuck in whatever visible state it was in the instant a puzzle/overlay opened,
    /// bleeding through behind it. Call this from PlayerInteractor.Pause().
    /// </summary>
    public void Hide()
    {
        if (cycleHintPanel != null) cycleHintPanel.SetActive(false);
    }

    /// <summary>
    /// Called by PlayerInteractor every frame with current in-range list and selected index.
    /// </summary>
    public void UpdateHint(List<InteractableBase> inRange, int selectedIndex)
    {
        if (cycleHintPanel == null) return;

        bool showHint = inRange.Count > 1;
        cycleHintPanel.SetActive(showHint);

        if (showHint && cycleHintLabel != null)
        {
            string name = inRange[selectedIndex].Handler?.InteractLabel ?? inRange[selectedIndex].gameObject.name;
            cycleHintLabel.text = $"< >  {selectedIndex + 1} / {inRange.Count}  -  {name}";
        }
    }
}