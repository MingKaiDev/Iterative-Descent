// PaintingPuzzleManager.cs
// Overworld painting ordering puzzle -- Story 18.
//
// MECHANIC:
//   Player walks to each painting and presses E on its button in the correct
//   sequence order (NEW first, then READY, RUNNING, etc.).
//   Correct press  -> that painting's light brightens. Move to next step.
//   Wrong press    -> all lights reset to original CFB595. Restart from step 1.
//   8th correct press -> all lights go green, puzzle solved.
//
// No confirm terminal needed. No rank cycling.
//
// Attach to any persistent or scene-level GO (e.g. Hallway2_Puzzles).
// Register all 8 PaintingInteractable components via the Inspector array.

using System;
using UnityEngine;

public class PaintingPuzzleManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static PaintingPuzzleManager Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────
    [Tooltip("All 8 PaintingInteractable components, in any order.")]
    public PaintingInteractable[] paintings = new PaintingInteractable[8];

    [Tooltip("ARBITEX commentary on wrong press. Optional.")]
    public DialogueSequence wrongOrderDialogue;

    [Tooltip("ARBITEX commentary on solve. Optional.")]
    public DialogueSequence solvedDialogue;

    // ── Event ──────────────────────────────────────────────────────────────
    /// <summary>Fired on solve. Param = total wrong presses.</summary>
    public static event Action<int> OnPaintingSolved;

    // ── State ──────────────────────────────────────────────────────────────
    private int   _currentStep;    // 0-7; the next expected correctSlot is _currentStep + 1
    private int   _wrongAttempts;
    private bool  _solved;
    private float _startTime;

    // ── Unity ──────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _startTime = Time.realtimeSinceStartup;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PaintingInteractable.Interact() when the player presses a button.
    /// </summary>
    public void OnPaintingPressed(PaintingInteractable painting)
    {
        if (_solved) return;

        int expectedSlot = _currentStep + 1;

        if (painting.CorrectSlot == expectedSlot)
        {
            // Correct press
            painting.OnCycled();
            _currentStep++;

            Debug.Log($"[Painting] Step {_currentStep}/8 correct -- {painting.name}");

            if (_currentStep >= 8)
                Solve();
        }
        else
        {
            // Wrong press
            _wrongAttempts++;
            _currentStep = 0;

            Debug.Log($"[Painting] Wrong press -- attempt {_wrongAttempts}. Resetting.");

            foreach (var p in paintings)
                p?.OnWrong();

            if (wrongOrderDialogue != null)
                DialogueManager.Instance?.Enqueue(wrongOrderDialogue);
        }
    }

    // ── Internal ───────────────────────────────────────────────────────────
    private void Solve()
    {
        _solved = true;
        Debug.Log($"[Painting] Puzzle solved! Wrong attempts: {_wrongAttempts}");

        foreach (var p in paintings)
            p?.OnCorrect();

        if (solvedDialogue != null)
            DialogueManager.Instance?.Enqueue(solvedDialogue);

        OnPaintingSolved?.Invoke(_wrongAttempts);
    }

    // ── Query ──────────────────────────────────────────────────────────────
    public bool  IsSolved    => _solved;
    public float ElapsedTime => Time.realtimeSinceStartup - _startTime;
}
