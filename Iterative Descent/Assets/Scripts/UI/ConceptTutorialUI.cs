// ConceptTutorialUI.cs
// HUD-style tutorial panel shown once per BKT concept tag, the first time the
// player is about to be quizzed on that concept. Same trigger point and same
// "once per concept per session" behaviour as the old dialogue-based
// ConceptPrimers, but built as a plain graphical panel (like HelpPanelUI's
// "How To Play" panel) instead of an ARBITEX dialogue line.
//
// SCENE SETUP
// -----------
//   1. Add the "Concept Tutorial Panel" prefab as a child of the Canvas
//      (inactive by default -- Awake() force-disables it anyway).
//   2. titleText, captionText, diagramRoot and closeButton are already wired
//      in the prefab. Re-wire in the Inspector only if you rebuild the panel
//      from scratch.
//   3. diagramRoot must contain one child GameObject PER concept, and that
//      child's name must exactly match the BKT concept tag string used in
//      quiz_bank.json (e.g. "arrays_and_lists"). Only one concept has a
//      diagram right now -- add more children as more diagrams get built.
//      Any concept tag with no matching child is silently skipped: no crash,
//      no panel, straight through to the quiz.
//
// PAUSE / CURSOR OWNERSHIP -- READ BEFORE CALLING THIS FROM SOMEWHERE NEW
// ------------------------------------------------------------------------
// This panel does NOT call PlayerInteractor.Resume() or reset the cursor /
// Time.timeScale when it closes. It relies on whatever onDone callback the
// caller passes in to immediately take over and re-establish paused state
// (that is exactly what PuzzleProp.OpenPuzzleUI does). This is intentional:
// PuzzleProp already calls PlayerInteractor.Pause() before this panel ever
// shows, and Hide() -> onDone -> OpenPuzzleUI() all happen synchronously in
// one call stack, so the player never gets an actual frame in an
// inconsistent state. If you ever call ShowIfUnseenThenContinue from
// somewhere whose onDone does NOT open another paused overlay, the game will
// stay paused with the cursor unlocked. Don't do that without revisiting
// this comment.
//
// USAGE (see ConceptTutorials.cs / PuzzleProp.OpenPuzzle)
//   ConceptTutorialUI.Instance.HasContentFor(conceptTag)
//   ConceptTutorialUI.Instance.Show(conceptTag, captionText, onClosed)

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConceptTutorialUI : MonoBehaviour, ICloseable
{
    public static ConceptTutorialUI Instance { get; private set; }

    [Header("Panel Content")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private Button closeButton;

    [Header("Per-Concept Diagrams")]
    [Tooltip("Container whose children are the per-concept diagrams. Each " +
             "child GameObject's name must exactly match a BKT concept tag.")]
    [SerializeField] private Transform diagramRoot;

    private bool _isOpen;
    private Action _onClosed;

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// True if diagramRoot has a child GameObject whose name exactly matches
    /// conceptTag. Callers should skip Show() entirely (and just continue)
    /// when this is false.
    /// </summary>
    public bool HasContentFor(string conceptTag)
    {
        if (diagramRoot == null || string.IsNullOrEmpty(conceptTag)) return false;
        return diagramRoot.Find(conceptTag) != null;
    }

    public void Show(string conceptTag, string caption, Action onClosed)
    {
        if (_isOpen) return;

        Transform match = diagramRoot != null ? diagramRoot.Find(conceptTag) : null;
        if (match == null)
        {
            // No diagram built for this concept yet -- don't show an empty panel.
            onClosed?.Invoke();
            return;
        }

        foreach (Transform child in diagramRoot)
            child.gameObject.SetActive(child == match);

        if (titleText != null) titleText.text = FormatTitle(conceptTag);
        if (captionText != null) captionText.text = caption;

        _onClosed = onClosed;
        _isOpen = true;
        gameObject.SetActive(true);

        PlayerInteractor.Pause();
        PlayerInteractor.RegisterCloseable(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void Hide()
    {
        if (!_isOpen) return;
        _isOpen = false;

        gameObject.SetActive(false);
        PlayerInteractor.DeregisterCloseable();
        // Deliberately NOT calling PlayerInteractor.Resume() or touching the
        // cursor / Time.timeScale here -- see the pause-ownership note above.

        Action cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    // ICloseable -- invoked by PlayerInteractor.Update() when ESC is pressed
    // while this panel is the registered closeable.
    public void Close() => Hide();

    // Words that should render fully uppercase in a title instead of just
    // Title-Case (e.g. "Bfs Dfs" -> "BFS DFS"). Add to this set rather than
    // hardcoding a title override per concept tag -- keeps FormatTitle
    // generic for the other 11 tags that don't need it.
    private static readonly HashSet<string> AcronymWords = new HashSet<string> { "bfs", "dfs", "cpu" };

    private static string FormatTitle(string conceptTag)
    {
        string[] words = conceptTag.Replace('_', ' ').Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length == 0) continue;
            words[i] = AcronymWords.Contains(words[i].ToLowerInvariant())
                ? words[i].ToUpperInvariant()
                : char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
        }
        return string.Join(" ", words);
    }
}
