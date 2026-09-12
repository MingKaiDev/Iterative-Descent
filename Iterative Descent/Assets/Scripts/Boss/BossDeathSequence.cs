// BossDeathSequence.cs
// Coordinates what happens after the boss dies: a short pause, then the
// front (courtyard) gate unlocks, then the end-of-demo panel shows.
//
// Deliberately its own script rather than added to BossHealth/BossStateMachine/
// BossHUD -- this project's convention for OnBossDeath is "many independent
// listeners self-subscribe" (see BossHUD, BossStateMachine), not one script
// that knows about everything. This one owns exactly one job: the post-death
// story beat (gate + end screen), nothing else.
//
// 2026-09-12 FIX: "end-of-demo panel never appears after a Level 1 -> Level 2 ->
// Level 1 round trip" (panel worked fine on a fresh/first Level 1 load). Root cause
// was the same "stale serialized reference into the persisted Canvas" bug already
// found and fixed project-wide for every OTHER Level-1 puzzle panel on 2026-09-11
// (see project-level-transition.md) -- endOfDemoUI just hadn't been converted yet,
// since BossDeathSequence/EndOfDemoUI predate that transition system entirely
// (built 2026-08-12/15). endOfDemoUI below is now resolved dynamically via
// EndOfDemoUI.Instance (with this field kept only as an optional same-scene
// override, same convention as every other cross-scene prop in this project) instead
// of relying solely on whatever the scene deserializer wires it to at load time.
// frontGate was NOT changed -- Door_South is an ordinary scene-local object with
// nothing else competing to destroy a fresh copy of it (unlike the Canvas, which has
// PersistentUIRoot's dedupe guard), so a plain reference re-wired fresh on every
// Level 1 load is not exposed to the same failure mode. Flag it for a look too if a
// gate-not-unlocking report ever comes in specifically after a return-trip fight,
// since that would mean this script itself is attached to something that persists
// across the transition instead of reloading fresh with the rest of Level 1.
//
// SETUP:
//   1. Attach to any persistent GameObject (e.g. the boss root, or GameManager).
//   2. Assign frontGate: drag Door_South (the courtyard gate -- confirmed as
//      the correct target per project decision on 2026-08-12) and its
//      DoorController component.
//   3. Optional: assign endOfDemoUI only if you need to override which panel is
//      used in this specific scene -- normally leave it unassigned and it resolves
//      automatically via EndOfDemoUI.Instance.
//   4. Tune delayBeforeGateUnlock / delayBeforeEndScreen if the death
//      animation/HUD timing needs more breathing room.
//
// Uses WaitForSecondsRealtime so this still runs correctly even if something
// else sets Time.timeScale = 0f during the death beat (matches the
// realtimeSinceStartup convention used elsewhere in this project for timers
// that must survive a paused game -- see feedback-unity-gotchas.md).
using System.Collections;
using UnityEngine;

public class BossDeathSequence : MonoBehaviour
{
    [Header("Front Gate")]
    [Tooltip("DoorController on Door_South (the courtyard gate). Unlock() is called after the delay below.")]
    [SerializeField] private DoorController frontGate;

    [Header("End Of Demo UI")]
    [Tooltip("Optional same-scene override. Normally leave unassigned -- resolved automatically " +
             "via EndOfDemoUI.Instance (see class doc comment for why this can no longer be a plain " +
             "required Inspector reference).")]
    [SerializeField] private EndOfDemoUI endOfDemoUI;

    /// <summary>
    /// Optional override first (rare -- only if this script and the panel are ever authored
    /// together in the same scene), else EndOfDemoUI.Instance, which itself falls back to a
    /// FindFirstObjectByType(..., FindObjectsInactive.Include) bootstrap the first time it's
    /// needed. Same resolution shape as every other cross-scene prop-to-panel link in this
    /// project (see BstPuzzleProp.ResolvePuzzleUI() and friends).
    /// </summary>
    private EndOfDemoUI ResolveEndOfDemoUI() => endOfDemoUI != null ? endOfDemoUI : EndOfDemoUI.Instance;

    [Header("Timing")]
    [Tooltip("Seconds to wait after death before the gate unlocks. Matches BossHUD's own 2s hide delay so the HUD, death animation, and gate don't all happen on top of each other.")]
    [SerializeField] private float delayBeforeGateUnlock = 3f;

    [Tooltip("Additional seconds after the gate unlocks before the end-of-demo panel appears.")]
    [SerializeField] private float delayBeforeEndScreen = 1.5f;

    private void Awake()
    {
        BossHealth.OnBossDeath += HandleBossDeath;
    }

    private void OnDestroy()
    {
        BossHealth.OnBossDeath -= HandleBossDeath;
    }

    private void HandleBossDeath()
    {
        StartCoroutine(DeathSequenceRoutine());
    }

    private IEnumerator DeathSequenceRoutine()
    {
        yield return new WaitForSecondsRealtime(delayBeforeGateUnlock);

        if (frontGate != null)
            frontGate.Unlock();
        else
            Debug.LogWarning("[BossDeathSequence] frontGate not assigned -- gate will not unlock.");

        yield return new WaitForSecondsRealtime(delayBeforeEndScreen);

        var resolvedEndOfDemoUI = ResolveEndOfDemoUI();
        if (resolvedEndOfDemoUI != null)
            resolvedEndOfDemoUI.Show();
        else
            Debug.LogWarning("[BossDeathSequence] Could not resolve an EndOfDemoUI instance -- " +
                              "end-of-demo panel will not appear. Make sure the End Of Demo Canvas " +
                              "exists somewhere in the scene (it can stay inactive).");
    }
}
