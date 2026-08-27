using UnityEngine;

/// <summary>
/// Listens for ReagentRoutingUI.OnReagentRoutingSolved and permanently equips
/// the player with a pistol barrel attachment: reduces PlayerCombat.settleTime
/// by settleTimeReduction seconds, so the aim reticle reaches full accuracy
/// faster.
///
/// DESIGN HISTORY (this reward has changed twice across the design conversation)
/// ------------------------------------------------------------------------
/// Round 1: replaced an earlier chemistryLabDoor placeholder with a
/// MatchingEventHandler-style guaranteed ammo+health drop via ItemSpawner.
/// Round 2 (this version): replaced that with a permanent barrel attachment
/// per an explicit "bigger reward" request. Confirmed to REPLACE the
/// ammo+health drop, not stack with it, and to auto-grant on solve rather
/// than spawn a physical pickup (no new prefab/art needed).
///
/// A separate idea -- tying reticle size (AccuracyT) to bonus damage, so a
/// tighter/more-settled shot also hits harder -- was raised but explicitly
/// put on hold; it is NOT implemented here. See the TODO comment next to
/// AccuracyT in PlayerCombat.cs for where that would hook in later.
///
/// RULES (same as DrainEventHandler / StackEventHandler)
/// ------------------------------------------------------------------------
///   - Attach to a PERSISTENT GameObject in the scene (e.g. GameManager).
///   - Do NOT add duplicate instances.
///
/// ONE-TIME GRANT GUARD (more critical here than for a consumable drop)
/// ------------------------------------------------------------------------
/// This is a PERMANENT stat change, not a consumable. The Chemistry Lab
/// terminal is currently re-enterable and re-solvable (confirmed in
/// conversation -- a proper "already solved" gate on the room itself is a
/// planned future task, not built yet). Without this guard, re-solving
/// would keep shaving time off settleTime every time, eventually making
/// aim settle instantly. _rewardGranted makes the upgrade apply exactly
/// once per play session. Remove it once the room-level gate exists.
/// </summary>
public class ReagentRoutingEventHandler : MonoBehaviour
{
    [Header("Barrel Attachment Reward")]
    [Tooltip("Seconds shaved off PlayerCombat.settleTime on solve -- makes the reticle reach full accuracy faster.")]
    [SerializeField] private float settleTimeReduction = 0.5f;

    private bool _rewardGranted;

    private void OnEnable()  => ReagentRoutingUI.OnReagentRoutingSolved += HandleSolved;
    private void OnDisable() => ReagentRoutingUI.OnReagentRoutingSolved -= HandleSolved;

    private void HandleSolved(int wrongAttempts)
    {
        Debug.Log($"[ReagentRoutingEventHandler] '{gameObject.name}' in scene '{gameObject.scene.name}'" +
                  $" -- Reagent Routing puzzle solved! Failed attempts: {wrongAttempts}");

        if (_rewardGranted)
        {
            Debug.Log("[ReagentRoutingEventHandler] Reward already granted this session -- " +
                      "skipping (terminal is currently re-solvable; a room-level gate is a planned future task).");
            return;
        }

        PlayerCombat combat = FindFirstObjectByType<PlayerCombat>();
        if (combat == null)
        {
            Debug.LogWarning("[ReagentRoutingEventHandler] No PlayerCombat found in scene -- barrel attachment not applied.");
            return;
        }

        combat.ReduceSettleTime(settleTimeReduction);
        _rewardGranted = true;
    }
}
