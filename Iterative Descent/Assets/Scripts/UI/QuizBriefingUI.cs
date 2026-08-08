// QuizBriefingUI.cs
// Simple reusable "why am I doing this" notice panel shown before a specific
// quiz opens, driven directly by a plain caption string from the caller.
//
// This is deliberately separate from ConceptTutorialUI, which is keyed to
// BKT concept tags, carries a diagram per tag, and only ever shows once per
// tag per session. QuizBriefingUI has no such bookkeeping -- it shows every
// time PuzzleProp.OpenPuzzle() runs on a prop that has briefingMessage set
// (see PuzzleProp.cs). Most PuzzleProp instances leave that field blank and
// never trigger this panel at all.
//
// PAUSE / CURSOR OWNERSHIP -- same contract as ConceptTutorialUI: this panel
// does NOT call PlayerInteractor.Resume() or reset the cursor / Time.timeScale
// on close. It relies on the caller's onContinue callback to immediately take
// over and re-establish paused state (see PuzzleProp.OpenPuzzle, which calls
// PlayerInteractor.Pause() before this panel ever shows, and chains straight
// into ConceptTutorials / OpenPuzzleUI afterwards). Don't call Show() from
// somewhere whose onContinue does not open another paused overlay without
// revisiting this note -- the game will stay paused with the cursor unlocked.
//
// SCENE SETUP -- see FYP/setup-guides/QuizBriefing_UnitySetup.md

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuizBriefingUI : MonoBehaviour, ICloseable
{
    public static QuizBriefingUI Instance { get; private set; }

    [Header("Panel Content")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button continueButton;

    private bool _isOpen;
    private Action _onContinue;

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
        if (continueButton != null)
            continueButton.onClick.AddListener(Hide);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Shows the panel with the given message, then invokes onContinue once
    /// the player dismisses it. If message is null/empty, or the panel is
    /// already open, onContinue fires immediately instead -- callers never
    /// need to check HasContentFor() style guards themselves.
    /// </summary>
    public void Show(string message, Action onContinue)
    {
        if (_isOpen || string.IsNullOrEmpty(message))
        {
            onContinue?.Invoke();
            return;
        }

        if (messageText != null) messageText.text = message;

        _onContinue = onContinue;
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

        Action cb = _onContinue;
        _onContinue = null;
        cb?.Invoke();
    }

    // ICloseable -- invoked by PlayerInteractor.Update() when ESC is pressed
    // while this panel is the registered closeable.
    public void Close() => Hide();
}
