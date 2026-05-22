// Quiz1EventHandler.cs
// NOTE: This file should be renamed to Quiz1EventHandler.cs in the Unity Editor
//       (right-click in Project window -> Rename). Unity will remap the scene reference.
using UnityEngine;

/// <summary>
/// Event handler for the main hall Quiz 1 prop.
/// "Quiz1" refers specifically to the MCQ questionnaire tied to the main hall computer prop.
/// Future quiz props should have their own numbered handler (Quiz2EventHandler, etc.)
/// to avoid naming conflicts.
///
/// PlayerMetricsTracker already subscribes to OnPuzzleFinished automatically,
/// so you only need to call NotifyQuizStarted() when the quiz opens.
/// </summary>
public class Quiz1EventHandler : MonoBehaviour
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
        Debug.Log($"[Quiz1EventHandler] Quiz closed. Passed: {passed}");

        // Add any game logic here (unlock door, play animation, etc.)
    }

    // ── Note: No need to subscribe to OnPuzzleFinished here anymore ────────
    // PlayerMetricsTracker.OnEnable() handles that subscription directly.
}