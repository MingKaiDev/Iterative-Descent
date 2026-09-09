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
//   4. bossAudio is optional -- the BossAudio component on the boss root.
//      Leave unassigned to skip the encounter stinger and boss music. When
//      assigned, this trigger also starts BossAudio's looping boss music
//      (BossAudio.bossMusicClip), which hard-swaps with GameAudioManager's
//      ambient loop and stops automatically when the boss dies.
//   5. bossIntroDialogue is optional -- assign Arb_BossIntro (Assets/Dialogue/
//      ARBITEX/Arb_BossIntro.asset) to have ARBITEX speak the moment the
//      player commits to the arena. Uses DialogueManager.Interrupt() so it
//      cuts off any straggling reactive commentary and plays immediately.
//      Does not pause the player -- the line has no choices, so it plays
//      over the top as combat begins.
//
//   6. arenaDoors is optional -- assign the DoorController(s) on the arena
//      entrance. It is a DOUBLE door, so this needs TWO DoorController
//      components in the scene (one per leaf, e.g. Door_South_Left /
//      Door_South_Right -- each on its own GameObject with its own doorMesh
//      assigned) -- drag both into this array. When assigned, this trigger
//      calls CloseAndLock() on every entry the moment the player commits,
//      sealing both leaves shut (and re-enabling their colliders) even if a
//      puzzle had already opened them earlier. This is permanent for the
//      rest of the fight -- the doors do not reopen when the boss dies.
//
// One-shot: fires once per scene load, re-entering the trigger does nothing.
using UnityEngine;

public class BossEncounterTrigger : MonoBehaviour
{
    [Header("Boss (optional -- only if it starts inactive)")]
    [Tooltip("Leave unassigned if the boss is already active and relying on its own detectionRadius.")]
    [SerializeField] private GameObject aresGameObject;

    [Header("Boss HUD")]
    [Tooltip("BossHUD starts inactive by design -- this trigger shows it when the player enters the arena.")]
    [SerializeField] private BossHUD bossHUD;

    [Header("Boss Audio (optional)")]
    [Tooltip("BossAudio component on the boss root. Leave unassigned to skip the encounter stinger.")]
    [SerializeField] private BossAudio bossAudio;

    [Header("Boss Intro Dialogue (optional)")]
    [Tooltip("Assign Arb_BossIntro.asset. Plays through DialogueManager.Interrupt() -- cuts off " +
             "any other dialogue and plays immediately, without pausing the player.")]
    [SerializeField] private DialogueSequence bossIntroDialogue;

    [Header("Arena Gate (optional)")]
    [Tooltip("DoorController(s) on the arena entrance -- it's a double door, so this needs BOTH " +
             "leaves' DoorController components (one per leaf). Leave empty to skip sealing the " +
             "arena. Sealed permanently -- does not reopen when the boss dies.")]
    [SerializeField] private DoorController[] arenaDoors;

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

        if (arenaDoors != null && arenaDoors.Length > 0)
        {
            foreach (var door in arenaDoors)
                door?.CloseAndLock();
        }
        else
        {
            Debug.LogWarning("[BossEncounterTrigger] arenaDoors not assigned -- player can still walk back out of the arena.");
        }

        if (bossHUD != null)
            bossHUD.ShowHUD();
        else
            Debug.LogWarning("[BossEncounterTrigger] bossHUD not assigned -- HUD will not appear.");

        if (bossAudio != null)
        {
            bossAudio.PlayEncounterStinger();
            bossAudio.PlayBossMusic();
        }

        if (bossIntroDialogue != null)
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Interrupt(bossIntroDialogue);
            else
                Debug.LogWarning("[BossEncounterTrigger] DialogueManager.Instance is null -- intro line will not play.");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.2f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
