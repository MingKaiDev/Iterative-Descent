// BossEncounterTrigger.cs
// Minimal version. The boss already handles its own player-proximity
// activation (see BossStateMachine.detectionRadius / UpdateIdle -- it
// transitions Idle -> Combat on its own once the player is close enough).
// This trigger does NOT spawn or activate the boss itself. Its only job is
// to show the boss HUD when the player commits to the arena, since BossHUD
// starts inactive and its own doc comment already says "call ShowHUD() from
// BossEncounterTrigger when the fight begins."
//
// SETUP:
//   1. Place on a trigger-collider child GameObject at the arena entrance
//      (BoxCollider, Is Trigger = true).
//   2. Assign bossHUD (the BossHUD component on BossHUDCanvas).
//   3. aresGameObject is optional -- only needed if you want the boss root
//      GameObject explicitly SetActive(true) on trigger (e.g. if it starts
//      disabled for pacing/performance reasons). Leave it unassigned if the
//      boss is already active in the scene and just relying on its own
//      detectionRadius.
//
// One-shot: fires once per scene load, re-entering the trigger does nothing.
// No arena gate / lock logic yet -- not part of this pass.
using UnityEngine;

public class BossEncounterTrigger : MonoBehaviour
{
    [Header("Boss (optional -- only if it starts inactive)")]
    [Tooltip("Leave unassigned if the boss is already active and relying on its own detectionRadius.")]
    [SerializeField] private GameObject aresGameObject;

    [Header("Boss HUD")]
    [Tooltip("BossHUD starts inactive by design -- this trigger shows it when the player enters the arena.")]
    [SerializeField] private BossHUD bossHUD;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";

    private bool _triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag(playerTag)) return;

        _triggered = true;

        if (aresGameObject != null && !aresGameObject.activeSelf)
            aresGameObject.SetActive(true);

        if (bossHUD != null)
            bossHUD.ShowHUD();
        else
            Debug.LogWarning("[BossEncounterTrigger] bossHUD not assigned -- HUD will not appear.");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.2f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
