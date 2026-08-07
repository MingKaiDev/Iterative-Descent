// ConceptPrimers.cs
// Plays a short, one-time explainer line (via the existing DialogueManager) the
// first time the player is about to be quizzed on a given BKT concept tag.
//
// Why this exists:
// The quiz bank (quiz_bank.json) tests real CS/OS/networking vocabulary
// (Big-O, BST, CIDR, hash collisions, etc.) that a first-time or non-CS
// player has no way to already know. This does not change the quiz questions
// themselves (that would move the difficulty tiers mid-study) -- it just gives
// the player one sentence of context before they are tested on a topic for
// the first time.
//
// Content lives at Assets/Resources/Dialogue/ConceptPrimers/<conceptTag>.asset
// (a DialogueSequence, same asset type used by the rest of the dialogue system)
// and is loaded by exact conceptTag name match, so no manual Inspector wiring
// is required -- add a new concept, drop in a matching-named asset, done.
//
// Usage (see PuzzleProp.OpenPuzzle):
//   if (ConceptPrimers.HasUnseenPrimer(conceptTag))
//       ConceptPrimers.PlayThenCallback(conceptTag, OpenPuzzleUI);
//   else
//       OpenPuzzleUI();

using System;
using System.Collections.Generic;
using UnityEngine;

public static class ConceptPrimers
{
    private const string ResourceFolder = "Dialogue/ConceptPrimers/";

    // Tracks which concept tags have already had their primer shown this
    // session (process lifetime). Intentionally NOT persisted to disk --
    // a fresh launch of the build (e.g. a new pretrial participant) should
    // see every primer again.
    private static readonly HashSet<string> _shown = new HashSet<string>();

    private static Action _pendingCallback;

    /// <summary>
    /// True if this concept has a primer asset available AND it has not been
    /// shown yet this session. False for empty/unknown tags or already-seen ones.
    /// </summary>
    public static bool HasUnseenPrimer(string conceptTag)
    {
        if (string.IsNullOrEmpty(conceptTag)) return false;
        if (_shown.Contains(conceptTag)) return false;
        return Resources.Load<DialogueSequence>(ResourceFolder + conceptTag) != null;
    }

    /// <summary>
    /// Plays the primer for conceptTag (if one exists and hasn't been shown),
    /// then invokes onDone once the dialogue finishes. If there is no primer
    /// for this tag, onDone fires immediately.
    /// </summary>
    public static void PlayThenCallback(string conceptTag, Action onDone)
    {
        DialogueSequence sequence = string.IsNullOrEmpty(conceptTag)
            ? null
            : Resources.Load<DialogueSequence>(ResourceFolder + conceptTag);

        if (sequence == null)
        {
            onDone?.Invoke();
            return;
        }

        _shown.Add(conceptTag);

        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[ConceptPrimers] DialogueManager.Instance is null -- skipping primer, opening puzzle directly.");
            onDone?.Invoke();
            return;
        }

        _pendingCallback = onDone;
        DialogueManager.OnDialogueEnded += HandleDialogueEnded;
        DialogueManager.Instance.Enqueue(sequence);
    }

    private static void HandleDialogueEnded()
    {
        DialogueManager.OnDialogueEnded -= HandleDialogueEnded;
        Action cb = _pendingCallback;
        _pendingCallback = null;
        cb?.Invoke();
    }

    /// <summary>
    /// Clears the "already shown" set so primers replay from scratch.
    /// Call this from a "New Game" / restart flow if the project ever adds one
    /// mid-process (e.g. a test harness that resets state without relaunching).
    /// </summary>
    public static void ResetSession() => _shown.Clear();
}
