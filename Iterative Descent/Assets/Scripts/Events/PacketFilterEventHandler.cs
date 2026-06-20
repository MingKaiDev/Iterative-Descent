using UnityEngine;

/// <summary>
/// Listens for PacketFilterPuzzleUI.OnPacketFilterSolved and fires ARBITEX commentary.
///
/// BOSS CONSEQUENCE
/// ----------------
/// ServerNodeDisabled is a static bool read by the ARES boss fight.
/// If false when the ARES Phase 2 encounter starts, ARES gains:
///   - +25% maximum HP in Phase 2
///   - +10% damage output (all attacks)
/// This is hardcoded in the boss controller -- not DDA-driven.
///
/// RULES (same as DrainEventHandler / MatchingEventHandler)
/// ---------------------------------------------------------
///   - Attach to a PERSISTENT GameObject in the scene (e.g. GameManager).
///   - Do NOT add duplicate instances.
///   - Wire 'arbitexCommentator' if you want solve dialogue to fire.
///     Leave unassigned to skip the commentary trigger.
/// </summary>
public class PacketFilterEventHandler : MonoBehaviour
{
    // ── Static Boss Flag ────────────────────────────────────────────────────────

    /// <summary>
    /// True once the Packet Filter puzzle has been solved.
    /// Read by the ARES boss controller to determine Phase 2 stat modifiers.
    /// Resets to false on application start (intentional -- each session is fresh).
    /// </summary>
    public static bool ServerNodeDisabled { get; private set; }

    // ── Inspector ────────────────────────────────────────────────────────────────

    [Header("ARBITEX Commentary (optional)")]
    [Tooltip("Drag the ARBITEX commentator DialogueTrigger here if you want " +
             "the solve to fire a reaction line.")]
    [SerializeField] private DialogueTrigger arbitexCommentator;

    [Tooltip("Dialogue asset to play when the puzzle is solved. " +
             "Leave empty if using the default ARBITEX commentary system.")]
    [SerializeField] private DialogueSequence solveDialogue;

    // ── Runtime ──────────────────────────────────────────────────────────────────

    private bool _handled;

    // ── Unity ────────────────────────────────────────────────────────────────────

    private void OnEnable()  => PacketFilterPuzzleUI.OnPacketFilterSolved += HandleSolved;
    private void OnDisable() => PacketFilterPuzzleUI.OnPacketFilterSolved -= HandleSolved;

    // ── Handler ───────────────────────────────────────────────────────────────────

    private void HandleSolved(int wrongSubmissions)
    {
        if (_handled) return;
        _handled = true;

        ServerNodeDisabled = true;

        Debug.Log($"[PacketFilterEventHandler] '{gameObject.name}' -- " +
                  $"Packet Filter solved! Wrong commits: {wrongSubmissions}. " +
                  "ARBITEX server node offline. Boss HP/DMG modifiers disabled.");

        // Fire ARBITEX commentary reaction if wired
        if (arbitexCommentator != null)
        {
            arbitexCommentator.PlayDialogue();
        }
    }
}
