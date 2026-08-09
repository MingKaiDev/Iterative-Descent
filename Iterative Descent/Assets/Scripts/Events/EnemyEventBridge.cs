using UnityEngine;
using System.Collections;

/// <summary>
/// Listens for LinkedListPuzzleUI.OnLinkedListSolved (the same event that
/// LinkedListEventHandler uses to unlock the door) and triggers the enemy
/// break-in sequence with a configurable delay.
///
/// ─── Sequence ─────────────────────────────────────────────────────────────
///   LinkedList solved
///     │
///     ├── LinkedListEventHandler  → door.Unlock()  (already exists)
///     │
///     └── EnemyEventBridge (this)
///           │
///           ├── [wait activationDelay seconds — door is animating]
///           ├── (STUB) play door-bash SFX / VFX / camera shake
///           └── enemy.Activate(playerTransform) → chase begins
///
/// ─── Scene setup ──────────────────────────────────────────────────────────
///   Attach to the same persistent GameObject as LinkedListEventHandler.
///   Assign enemy (EnemyChaser) and playerTransform in the Inspector.
///   activationDelay should match DoorController.animDuration + suspense gap.
/// </summary>
public class EnemyEventBridge : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The EnemyChaser in the Classroom that will start chasing.")]
    [SerializeField] private EnemyChaser enemy;
    [Tooltip("The player's root Transform (the one PlayerMovement is on).")]
    [SerializeField] private Transform   playerTransform;

    [Header("Timing")]
    [Tooltip("Seconds after the puzzle is solved before the enemy activates. " +
             "Set to DoorController.animDuration (1.0 s) + dramatic pause.")]
    [SerializeField] private float activationDelay = 2.0f;

    // Guard: fire once per scene load even if the event somehow fires twice
    private bool _triggered;

    // ─── Event wiring ─────────────────────────────────────────────────────────

    private void OnEnable()
    {
        LinkedListPuzzleUI.OnLinkedListSolved += HandleLinkedListSolved;
    }

    private void OnDisable()
    {
        LinkedListPuzzleUI.OnLinkedListSolved -= HandleLinkedListSolved;
    }

    // ─── Handler ──────────────────────────────────────────────────────────────

    private void HandleLinkedListSolved(int wrongAttempts)
    {
        if (_triggered) return;
        _triggered = true;

        StartCoroutine(BreakInSequence());
    }

    private IEnumerator BreakInSequence()
    {
        // ── Step 1: Wait for door animation + suspense ────────────────────────
        yield return new WaitForSeconds(activationDelay);

        // ── Step 2: Door-bash effect (STUB — wire up when SFX/VFX are ready) ──
        Debug.Log("[EnemyEventBridge] STUB — play door-bash SFX + camera shake here.");
        // Examples to hook in later:
        //   AudioSource.PlayClipAtPoint(doorBashClip, transform.position);
        //   CameraShake.Instance.Shake(0.3f, 0.4f);

        // ── Step 3: Activate the enemy ────────────────────────────────────────
        if (enemy == null || playerTransform == null)
        {
            Debug.LogWarning("[EnemyEventBridge] enemy or playerTransform is null. " +
                             "Assign both in the Inspector.");
            yield break;
        }

        enemy.Activate(playerTransform);
        Debug.Log("[EnemyEventBridge] Enemy activated — the chase begins.");

        // This is Combat 1 -- the Main Hall door breach -- and chronologically
        // the FIRST enemy encounter any player reaches (before Classroom 1 /
        // Hallway 1, which use EncounterTrigger.cs). ShowCombatHintOnce() is
        // idempotent, so it's safe that EncounterTrigger also calls it for the
        // later encounters -- whichever fires first wins, which is now correctly
        // this one. See Story 31 / OnboardingHintUI.cs.
        OnboardingHintUI.Instance?.ShowCombatHintOnce();
    }

    // ─── Editor helper: visualise who we're wired to ─────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (enemy == null || playerTransform == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, enemy.transform.position);
        Gizmos.DrawLine(transform.position, playerTransform.position);
    }
}
