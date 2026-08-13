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
// SETUP:
//   1. Attach to any persistent GameObject (e.g. the boss root, or GameManager).
//   2. Assign frontGate: drag Door_South (the courtyard gate -- confirmed as
//      the correct target per project decision on 2026-08-12) and its
//      DoorController component.
//   3. Assign endOfDemoUI: the EndOfDemoUI component on the End Of Demo Canvas.
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
    [SerializeField] private EndOfDemoUI endOfDemoUI;

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

        if (endOfDemoUI != null)
            endOfDemoUI.Show();
        else
            Debug.LogWarning("[BossDeathSequence] endOfDemoUI not assigned -- end-of-demo panel will not appear.");
    }
}
