using UnityEngine;
using System.Collections;

/// <summary>
/// Listens for HashTablePuzzleUI.OnHashTablePanelClosed and triggers the
/// "corpse turns out to be alive" ambush: disables the corpse prop, then calls
/// ActivateEncounter() on an EncounterTrigger holding the dormant Brute. Mirrors
/// EnemyEventBridge (LinkedList -> EnemyChaser) but for the Hash Table puzzle ->
/// EnemyBrute, with an extra corpse-swap step.
///
/// 2026-09-09: switched from calling bruteEnemy.Activate() directly to routing through
/// an EncounterTrigger's (now-public) ActivateEncounter(). Reason: Retryable/
/// ResetForRetry()/checkpoint rollback are wired up entirely inside EncounterTrigger.
/// Start() (spawn-transform capture, setting enemy.Retryable) and ResetEncounter() --
/// an enemy activated by a direct .Activate() call from this bridge, bypassing
/// EncounterTrigger entirely, would never get Retryable set and a CheckpointTrigger
/// would have nothing to reference to roll it back. Routing through a (colliderless --
/// see scene setup below) EncounterTrigger gets all of that for free.
///
/// IMPORTANT: this keys off OnHashTablePanelClosed for TIMING, NOT OnHashTableSolved.
/// OnHashTableSolved fires the instant the round finishes -- while the "LINE
/// CLEARED"/"LINE FAILED" feedback popup is still covering the screen and the
/// player hasn't clicked OK yet, which is player-paced, not timed.
/// OnHashTablePanelClosed fires once that popup is dismissed and the puzzle
/// overlay is actually gone (cursor re-locked, player looking at the room
/// again) -- the right moment for a jump-scare reveal.
///
/// GATING: the ambush only fires if the same attempt was also graded a PASS
/// via OnHashTableAttemptGraded (>= 50% sorted -- the same grade that unlocks
/// the door in HashTableEventHandler). A failed attempt closes the panel with
/// no ambush, so the player can simply retry the puzzle. (Earlier version of
/// this script fired on every panel close regardless of pass/fail, which
/// meant a failed attempt still spawned an active, chasing Brute even though
/// the door stayed locked -- fixed here.)
///
/// ─── Sequence ─────────────────────────────────────────────────────────────
///   HashTable puzzle finishes
///     │
///     ├── OnHashTableAttemptGraded(passed) -- cached by this script
///     │
///   HashTable panel closed
///     │
///     └── HashTableEnemyEventBridge (this)
///           │
///           ├── if last graded attempt was NOT a pass -> do nothing
///           ├── [wait activationDelay seconds -- small cinematic beat]
///           ├── corpseObject.SetActive(false)        -- it was never a corpse
///           ├── bruteEnemy.gameObject.SetActive(true) -- reveal it
///           └── bruteEnemy.Activate(playerTransform)  -> wakeup scream + chase begins
///
/// ─── Scene setup ──────────────────────────────────────────────────────────
///   1. Place the corpse mesh/sprite prop and the EnemyBrute prefab at (roughly)
///      the same spot in the room, with the Brute's GameObject INACTIVE by
///      default (unchecked in the Inspector) so only the corpse is visible.
///   2. Create an empty GameObject (no collider needed -- it's only used as a
///      roster/bookkeeping holder here, never physically walked into) with an
///      EncounterTrigger component. Assign the Brute to its enemies array and
///      leave resetOnCheckpointRespawn on (default) if you want this ambush to
///      re-fight after a checkpoint respawn, off if it should stay resolved once.
///   3. Attach this script to a persistent GameObject (same object as
///      HashTableEventHandler works fine). Assign corpseObject and bruteEncounter
///      (the EncounterTrigger from step 2) in the Inspector.
///   4. Tune activationDelay -- 0 fires the instant the panel closes; a small
///      value (0.3-0.8s) gives the player a beat to notice the room again
///      before the scream hits.
/// </summary>
public class HashTableEnemyEventBridge : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The corpse mesh/sprite prop. Disabled the moment the Brute reveals itself.")]
    [SerializeField] private GameObject corpseObject;
    [Tooltip("EncounterTrigger holding the dormant Brute in its enemies array -- needs no collider " +
             "of its own, this script calls its ActivateEncounter() directly. Routing through it " +
             "(instead of calling Activate() on the Brute directly) is what lets checkpoint rollback " +
             "(Retryable / ResetEncounter()) reach this Brute -- see class doc comment above.")]
    [SerializeField] private EncounterTrigger bruteEncounter;

    [Header("Timing")]
    [Tooltip("Seconds after the puzzle panel closes before the corpse swap + Brute activation happens. " +
             "0 = instant. A small value gives a cinematic beat before the scream.")]
    [SerializeField] private float activationDelay = 0.5f;

    // Guard: fire once per scene load even if the event somehow fires twice
    private bool _triggered;

    // Cached from the most recent OnHashTableAttemptGraded -- OnHashTableAttemptGraded
    // always fires before OnHashTablePanelClosed for the same attempt (grading happens
    // in CheckFinished()/RequestClose(), panel-close only after the popup is dismissed
    // or immediately after in the escape-hatch case), so by the time the panel-closed
    // handler below runs, this reflects whether THAT attempt was a pass.
    private bool _lastAttemptPassed;

    // ─── Event wiring ─────────────────────────────────────────────────────────

    private void OnEnable()
    {
        HashTablePuzzleUI.OnHashTableAttemptGraded += HandleAttemptGraded;
        HashTablePuzzleUI.OnHashTablePanelClosed += HandlePanelClosed;
        if (bruteEncounter != null) bruteEncounter.OnEncounterReset += HandleEncounterReset;
    }

    private void OnDisable()
    {
        HashTablePuzzleUI.OnHashTableAttemptGraded -= HandleAttemptGraded;
        HashTablePuzzleUI.OnHashTablePanelClosed -= HandlePanelClosed;
        if (bruteEncounter != null) bruteEncounter.OnEncounterReset -= HandleEncounterReset;
    }

    // ─── Handlers ─────────────────────────────────────────────────────────────

    private void HandleAttemptGraded(bool passed)
    {
        _lastAttemptPassed = passed;
    }

    // Mirrors bruteEncounter's own reset -- without this, this bridge's independent
    // _triggered guard stays true after a checkpoint respawn (even though the Brute and the
    // EncounterTrigger itself are correctly reset), so re-solving the puzzle silently does
    // nothing and the Brute never re-activates.
    private void HandleEncounterReset()
    {
        _triggered = false;
        _lastAttemptPassed = false;
    }

    private void HandlePanelClosed()
    {
        if (_triggered) return;

        if (!_lastAttemptPassed)
        {
            Debug.Log("[HashTableEnemyEventBridge] Panel closed but the attempt wasn't graded a pass " +
                      "(< 50% sorted) -- door stays locked, no ambush. Player can retry the puzzle.");
            return;
        }

        _triggered = true;
        StartCoroutine(AmbushSequence());
    }

    private IEnumerator AmbushSequence()
    {
        if (activationDelay > 0f)
            yield return new WaitForSeconds(activationDelay);

        // ── Step 1: Swap the corpse out ────────────────────────────────────────
        if (corpseObject != null)
            corpseObject.SetActive(false);
        else
            Debug.LogWarning("[HashTableEnemyEventBridge] corpseObject not assigned.", this);

        // ── Step 2: Reveal + activate the Brute (via its EncounterTrigger -- see class doc) ──
        if (bruteEncounter == null)
        {
            Debug.LogWarning("[HashTableEnemyEventBridge] bruteEncounter not assigned. " +
                             "Assign it in the Inspector.", this);
            yield break;
        }

        bruteEncounter.ActivateEncounter();
        Debug.Log("[HashTableEnemyEventBridge] Corpse swapped -- Brute activated, encounter begins.");

        // Idempotent -- safe even if an earlier encounter already showed this hint.
        OnboardingHintUI.Instance?.ShowCombatHintOnce();
    }

    // ─── Editor helper: visualise who we're wired to ─────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (bruteEncounter != null) Gizmos.DrawLine(transform.position, bruteEncounter.transform.position);
        if (corpseObject != null) Gizmos.DrawLine(transform.position, corpseObject.transform.position);
    }
}
