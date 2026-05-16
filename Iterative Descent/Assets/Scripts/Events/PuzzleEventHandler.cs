// PuzzleEventHandler.cs
using UnityEngine;

/// <summary>
/// Attach this to whatever GameObject opens your quiz panel (e.g. a computer interactable,
/// a door, or a puzzle trigger).
///
/// This replaces your original PuzzleEventHandler.
/// PlayerMetricsTracker already subscribes to OnPuzzleFinished automatically,
/// so you only need to call NotifyQuizStarted() when the quiz opens.
/// </summary>
public class PuzzleEventHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject puzzleUIPanel;  // drag your Quiz Canvas/Panel here
    [SerializeField] private PuzzleUI puzzleUI;       // drag your PuzzleUI component here
    [SerializeField] private QuestionData[] questions;  // drag your question set here

    // ── Open the quiz (call this from your interactable / trigger) ─────────
    public void OpenQuiz()
    {
        // 1. Notify metrics tracker so quiz timing starts
        if (PlayerMetricsTracker.Instance != null)
            PlayerMetricsTracker.Instance.NotifyQuizStarted();

        // 2. Show the UI and set it up as normal
        puzzleUIPanel.SetActive(true);
        puzzleUI.Setup(questions, OnQuizClosed);
    }

    // ── Called by PuzzleUI when the player clicks Close ───────────────────
    private void OnQuizClosed(bool passed)
    {
        puzzleUIPanel.SetActive(false);
        Debug.Log($"[PuzzleEventHandler] Quiz closed. Passed: {passed}");

        // Add any game logic here (unlock door, play animation, etc.)
    }

    // ── Note: No need to subscribe to OnPuzzleFinished here anymore ────────
    // PlayerMetricsTracker.OnEnable() handles that subscription directly.
}