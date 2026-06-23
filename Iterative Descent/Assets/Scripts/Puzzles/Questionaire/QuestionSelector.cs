using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Selects a subset of questions from the full question bank for each quiz session.
///
/// First session (isFirstSession == true):
///   Returns a predetermined mix of foundational concept questions to establish
///   a BKT baseline before the player encounters the LL and Scheduling puzzles.
///   Default plan: 3 x arrays_and_lists + 2 x complexity_big_o = 5 questions.
///
/// Subsequent sessions:
///   Reads BKT state from PlayerMetricsTracker.Instance.BKT and targets the
///   concept with the lowest P(knows) that has questions available in the bank.
///   Returns up to questionsPerSession questions for that concept, shuffled.
///
/// If BKT is unavailable or no concept has questions, falls back to a random
/// sample across all tagged questions.
/// </summary>
public static class QuestionSelector
{
    // ── First Session Plan ────────────────────────────────────────────────────
    // Tunable here -- change concept keys or counts without touching PuzzleProp.
    // Total must equal questionsPerSession (default 5) or you will get fewer questions.

    private static readonly (string concept, int count)[] FirstSessionPlan =
    {
        (BayesianKnowledgeTracker.ArraysAndLists, 3),
        (BayesianKnowledgeTracker.ComplexityBigO, 2),
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a session-appropriate subset of questions from allQuestions.
    /// </summary>
    /// <param name="allQuestions">The full loaded question bank.</param>
    /// <param name="isFirstSession">True on the player's first quiz interaction globally (not per-prop).</param>
    /// <param name="questionsPerSession">How many questions to return per session.</param>
    /// <param name="pinnedConceptTag">
    /// When non-empty, bypasses first-session plan and BKT targeting entirely.
    /// Questions are drawn only from this concept, shuffled. Use this for
    /// contextual terminals that should always test a specific concept (e.g. bfs_dfs
    /// before the Drain Puzzle room).
    /// </param>
    public static QuestionData[] SelectQuestions(
        QuestionData[] allQuestions,
        bool           isFirstSession,
        int            questionsPerSession = 5,
        string         pinnedConceptTag   = "")
    {
        if (allQuestions == null || allQuestions.Length == 0)
        {
            Debug.LogWarning("[QuestionSelector] No questions available.");
            return allQuestions;
        }

        if (!string.IsNullOrEmpty(pinnedConceptTag))
        {
            QuestionData[] pinned = allQuestions
                .Where(q => q.conceptTag == pinnedConceptTag)
                .ToArray();

            if (pinned.Length == 0)
            {
                Debug.LogWarning($"[QuestionSelector] Pinned concept '{pinnedConceptTag}' has no questions -- falling back to BKT.");
            }
            else
            {
                QuestionData[] result = Shuffle(pinned).Take(questionsPerSession).ToArray();
                Debug.Log($"[QuestionSelector] Pinned concept '{pinnedConceptTag}' | Returning {result.Length} questions.");
                return result;
            }
        }

        QuestionData[] selected = isFirstSession
            ? SelectFirstSession(allQuestions)
            : SelectByBKT(allQuestions, questionsPerSession);

        Debug.Log($"[QuestionSelector] Session type: {(isFirstSession ? "first (predetermined)" : "BKT-driven")} | " +
                  $"Returning {selected.Length} questions.");

        return selected;
    }

    // ── First Session ─────────────────────────────────────────────────────────

    private static QuestionData[] SelectFirstSession(QuestionData[] allQuestions)
    {
        var selected = new List<QuestionData>();

        foreach ((string concept, int count) in FirstSessionPlan)
        {
            QuestionData[] pool = allQuestions
                .Where(q => q.conceptTag == concept)
                .ToArray();

            if (pool.Length == 0)
            {
                Debug.LogWarning($"[QuestionSelector] First session plan: no questions found for '{concept}'.");
                continue;
            }

            selected.AddRange(Shuffle(pool).Take(count));
        }

        if (selected.Count == 0)
        {
            Debug.LogWarning("[QuestionSelector] First session plan returned 0 questions -- falling back to random sample.");
            return Shuffle(allQuestions).Take(5).ToArray();
        }

        return selected.ToArray();
    }

    // ── BKT-Driven Session ────────────────────────────────────────────────────

    private static QuestionData[] SelectByBKT(QuestionData[] allQuestions, int count)
    {
        BayesianKnowledgeTracker bkt = PlayerMetricsTracker.Instance?.BKT;

        // All distinct concept tags present in the question bank
        string[] availableConcepts = allQuestions
            .Where(q => !string.IsNullOrEmpty(q.conceptTag))
            .Select(q => q.conceptTag)
            .Distinct()
            .ToArray();

        if (availableConcepts.Length == 0)
        {
            Debug.LogWarning("[QuestionSelector] No tagged questions found -- returning random sample.");
            return Shuffle(allQuestions).Take(count).ToArray();
        }

        string targetConcept;

        if (bkt == null || !bkt.HasAnyData())
        {
            // BKT has no data yet on this path -- pick a random available concept
            targetConcept = availableConcepts[Random.Range(0, availableConcepts.Length)];
            Debug.Log($"[QuestionSelector] BKT has no data -- random concept: '{targetConcept}'.");
        }
        else
        {
            // Pick the concept the player knows least well
            targetConcept = availableConcepts
                .OrderBy(c => bkt.GetKnowledgeState(c))
                .First();

            Debug.Log($"[QuestionSelector] Weakest concept: '{targetConcept}' " +
                      $"P(knows)={bkt.GetKnowledgeState(targetConcept):F3}");
        }

        QuestionData[] conceptPool = allQuestions
            .Where(q => q.conceptTag == targetConcept)
            .ToArray();

        if (conceptPool.Length == 0)
        {
            Debug.LogWarning($"[QuestionSelector] No questions for target concept '{targetConcept}' -- random fallback.");
            return Shuffle(allQuestions).Take(count).ToArray();
        }

        return Shuffle(conceptPool).Take(count).ToArray();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static T[] Shuffle<T>(T[] array)
    {
        T[] result = (T[])array.Clone();
        for (int i = result.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }
}
