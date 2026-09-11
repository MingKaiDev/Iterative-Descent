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
/// SOLVE DIALOGUE ("connection to ARES severed")
/// -----------------------------------------------
/// Two ways to wire the reaction line, checked in this order:
///   1. 'solveDialogue' -- a DialogueSequence assigned directly here. No extra
///      GameObject needed; just drag the asset in (see Assets/Dialogue/ARBITEX/
///      Arb_PacketFilterSolved.asset). Played via DialogueManager.Enqueue()
///      the instant the puzzle is solved. Takes priority when assigned.
///   2. 'arbitexCommentator' -- legacy path. Drag a scene DialogueTrigger here
///      (its own _sequence field holds the line) and this calls PlayDialogue()
///      on it instead. Only used when 'solveDialogue' is left empty.
/// Leave both unassigned to skip the reaction line entirely.
///
/// RULES (same as DrainEventHandler / MatchingEventHandler)
/// ---------------------------------------------------------
///   - Attach to a PERSISTENT GameObject in the scene (e.g. GameManager).
///   - Do NOT add duplicate instances.
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
    [Tooltip("Dialogue asset to play the instant the puzzle is solved -- e.g. ARBITEX " +
             "reacting to losing its connection to ARES. No DialogueTrigger GameObject " +
             "needed, just assign the asset (see Arb_PacketFilterSolved.asset). Takes " +
             "priority over Arbitex Commentator below when both are set.")]
    [SerializeField] private DialogueSequence solveDialogue;

    [Tooltip("Legacy alternative: drag a scene DialogueTrigger here to route the solve " +
             "reaction through its own assigned sequence instead. Ignored if Solve " +
             "Dialogue above is assigned.")]
    [SerializeField] private DialogueTrigger arbitexCommentator;

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

        // Fire the "connection to ARES severed" reaction line, if wired.
        if (solveDialogue != null)
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Enqueue(solveDialogue);
            else
                Debug.LogWarning("[PacketFilterEventHandler] DialogueManager.Instance is null -- solve dialogue will not play.", this);
        }
        else if (arbitexCommentator != null)
        {
            arbitexCommentator.PlayDialogue();
        }
    }
}
