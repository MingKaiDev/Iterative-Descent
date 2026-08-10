# Melee Hitbox Timing Fix — Editor Setup

Code-side fix is done (`EnemyAttack.cs` for CyberSoldier/EnemyChaser, `RusherAttack.cs` for EnemyRusher). Both now use a one-shot `Physics.OverlapBox` hit check instead of toggling a trigger collider, which removes the "missed physics tick" failure mode. The remaining accuracy problem — `hitboxDelay` being a guessed number instead of something tied to the real animation — needs the steps below, done in-editor.

## 1. CyberSoldier prefab cleanup (required, not optional)

`EnemyAttack.fistHitbox` changed type from `Collider` to `BoxCollider`. Unity nulls out a serialized field when its type changes, so the existing Inspector reference on the prefab is gone.

- Open `CyberSoldier.prefab`.
- Select the `EnemyAttack` component. Confirm `Fist Hitbox` shows empty.
- Drag the fist bone's `BoxCollider` back into that field. If there are two `FistHitBox` objects on the fist bone (there is a known duplicate, only one was ever wired up), pick the one that was previously assigned — check `git blame`/prefab history if unsure which, or just pick either since they were identical in size/position.
- Delete the other, unused `FistHitBox` GameObject (or its `BoxCollider` + `FistHitboxRelay`) — it's dead weight now that hits aren't detected via trigger events at all.
- Remove the `FistHitboxRelay` component from whichever `FistHitBox` remains. It's no longer called by anything (`EnemyAttack.OnFistHit()` was deleted).
- Confirm the `Hitable Layers` field on `EnemyAttack` is set to the Player layer (defaults to Everything, which works but is wider than necessary).

## 2. Rusher prefab check

`RusherAttack.cs`'s public API changed (new `OnRightHitFrame()`/`OnLeftHitFrame()` methods, `RunHitCheck` replaces inline logic) but no serialized field types changed, so `rightFistBox`/`leftFistBox` references should survive as-is. Open the Rusher prefab and confirm both are still assigned — just to be safe, since a missing reference here fails silently (logs "STUB -- side FistBox not assigned yet" instead of erroring).

## 3. Placing real Animation Events (the actual accuracy fix)

This is what makes hits land on the true impact frame instead of a guessed delay.

For each clip below, open it in the Animation window (double-click the clip, or select the enemy in Play mode and scrub), find the frame where the fist/hand visually connects, right-click the timeline at that frame → **Add Animation Event**, and set the function:

| Enemy | Clip | Event function | Frame |
|---|---|---|---|
| CyberSoldier | Zombie Punch 1 clip (driven by the `Punch1` trigger — see `CyberSoldier_AttackAnimation_UnitySetup.md`, this superseded the old single `Attack` trigger) | `OnAttackHitFrame` | impact frame |
| CyberSoldier | Zombie Punch 2 clip (`Punch2` trigger) | `OnAttackHitFrame` | impact frame |
| CyberSoldier | both clips, last frame | `OnAttackEnd` (optional) | last frame |
| Rusher | Right Swipe clip | `OnRightHitFrame` | impact frame |
| Rusher | Left Swipe clip | `OnLeftHitFrame` | impact frame |
| Rusher | both clips, last frame | `OnAttackEnd` (optional) | last frame |

Once an event is wired, the fixed `hitboxDelay` fallback becomes irrelevant for that clip (the event fires first and sets the "already checked" guard) — you don't need to also tune the delay for clips that have events.

## 4. If you skip step 3 for now

Both scripts still work via the `hitboxDelay` timer fallback — nothing is broken by not adding events yet. To tune the fallback delay instead: enter Play mode, trigger the attack, and watch the console for:

```
[EnemyAttack] HIT CHECK ACTIVE | delay=0.40s | time=12.34
[RusherAttack] HIT CHECK ACTIVE -- right hand | delay=0.45s | time=8.02
```

Compare the log's timing against when the fist visually connects in the Play window, then adjust `hitboxDelay` in the Inspector until they line up. This is what the log was added for.

## Not in scope for this pass

DDA currently scales `NavMeshAgent.speed` on both enemies but never `Animator.speed` or the attack timers (`hitboxDelay`, `attackCooldown`). At higher tiers, movement speeds up while attack timing/animation playback stays fixed — meaning the timing gap between visual telegraph and hit check (for clips still using the fallback delay, not events) can drift further at high tiers. Flagged as a follow-up, not fixed here — using real Animation Events (step 3) makes this a non-issue since events are tied to normalized clip position, not wall-clock time.
