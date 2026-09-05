using UnityEngine;
using System.Collections;

/// <summary>
/// Listens for HashTablePuzzleUI.OnHashTablePanelClosed and triggers the
/// "corpse turns out to be alive" ambush: disables the corpse prop, enables
/// the dormant Brute enemy hidden in its place, and activates it to start
/// the combat encounter. Mirrors EnemyEventBridge (LinkedList -> EnemyChaser)
/// but for the Hash Table puzzle -> EnemyBrute, with an extra corpse-swap step.
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
///   2. Attach this script to a persistent GameObject (same object as
///      HashTableEventHandler works fine).
///   3. Assign corpseObject, bruteEnemy, and playerTransform in the Inspector.
///   4. Tune activationDelay -- 0 fires the instant the panel closes; a small
///      value (0.3-0.8s) gives the player a beat to notice the room again
///      before the scream hits.
/// </summary>
public class HashTableEnemyEventBridge : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The corpse mesh/sprite prop. Disabled the moment the Brute reveals itself.")]
    [SerializeField] private GameObject corpseObject;
    [Tooltip("The dormant Brute enemy hidden in the corpse's place. Its GameObject should start " +
             "INACTIVE in the scene -- this script enables it, then calls Activate().")]
    [SerializeField] private EnemyBrute bruteEnemy;
    [Tooltip("The player's root Transform (the one PlayerMovement is on).")]
    [SerializeField] private Transform playerTransform;

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
    }

    private void OnDisable()
    {
        HashTablePuzzleUI.OnHashTableAttemptGraded -= HandleAttemptGraded;
        HashTablePuzzleUI.OnHashTablePanelClosed -= HandlePanelClosed;
    }

    // ─── Handlers ─────────────────────────────────────────────────────────────

    private void HandleAttemptGraded(bool passed)
    {
        _lastAttemptPassed = passed;
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

        // ── Step 2: Reveal + activate the Brute ────────────────────────────────
        if (bruteEnemy == null || playerTransform == null)
        {
            Debug.LogWarning("[HashTableEnemyEventBridge] bruteEnemy or playerTransform is null. " +
                             "Assign both in the Inspector.", this);
            yield break;
        }

        if (!bruteEnemy.gameObject.activeSelf)
            bruteEnemy.gameObject.SetActive(true);

        bruteEnemy.Activate(playerTransform);
        Debug.Log("[HashTableEnemyEventBridge] Corpse swapped -- Brute activated, encounter begins.");

        // Idempotent -- safe even if an earlier encounter already showed this hint.
        OnboardingHintUI.Instance?.ShowCombatHintOnce();
    }

    // ─── Editor helper: visualise who we're wired to ─────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (bruteEnemy != null) Gizmos.DrawLine(transform.position, bruteEnemy.transform.position);
        if (corpseObject != null) Gizmos.DrawLine(transform.position, corpseObject.transform.position);
    }
}
