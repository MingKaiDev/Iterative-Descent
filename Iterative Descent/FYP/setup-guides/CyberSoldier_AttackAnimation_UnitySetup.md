# CyberSoldier Attack Animation — Editor Setup

Two code-side features landed for CyberSoldier's attack: a windup rotation (already fully wired, no Editor steps needed beyond tuning), and alternating punch animations (needs the Animator Controller wired below — this is the only outstanding step).

## Windup rotation — no action needed, tuning only

`EnemyChaser.cs` now rotates the enemy away from facing you during the windup, then lets it naturally rotate back. Two Inspector fields on the CyberSoldier prefab under **Attack Windup**:

- `Windup Rotation Degrees` (default -30) — how far to rotate, viewed from above. If it visually rotates clockwise instead of anti-clockwise, flip the sign to +30.
- Duration is no longer a separate field — it's tied automatically to `EnemyAttack`'s `Hitbox Delay`, so the rotation always completes exactly when the swing resolves.

Nothing to wire here. Just playtest and adjust the degrees value to taste.

## Alternating punch animations — Animator Controller wiring required

The controller is `Assets/Mixamo/Enemies/Basic Enemy/Basic Zombie.controller`. Two new animation clips are already imported: **Zombie Punch 1** (from `Zombie Punching.fbx`) and **Zombie Punch 2** (from `Zombie Punching (1).fbx`), both in the same folder as the controller.

`EnemyChaser.cs` now fires two new Animator triggers alternately, `Punch1` and `Punch2` (configurable string fields on the prefab if you want different names), instead of the old single `Attack` trigger. You need to add these to the controller:

### 1. Add two Trigger parameters

In the Animator window's Parameters tab, click **+** → **Trigger**, twice:
- `Punch1`
- `Punch2`

(Match these exactly to the `Punch1 Trigger Name` / `Punch2 Trigger Name` fields on `EnemyChaser` if you rename them.)

### 2. Add two states

Right-click empty canvas space → **Create State → Empty**, twice. For each:
- Rename one to `Zombie Punch 1`, set its **Motion** to the `Zombie Punch 1` clip (inside `Zombie Punching.fbx`).
- Rename the other to `Zombie Punch 2`, set its **Motion** to the `Zombie Punch 2` clip (inside `Zombie Punching (1).fbx`).

### 3. Wire transitions in, matching the existing "Attack" pattern exactly

The existing `Walking` → `zombie attack` transition is the template — right-click `Walking` → **Make Transition** → drag to `Zombie Punch 1`, then again to `Zombie Punch 2`. For each new transition, set:
- **Condition**: `Punch1` (or `Punch2` for the other)
- **Has Exit Time**: unchecked
- **Transition Duration**: 0.25
- **Can Transition To Self**: checked

This exactly matches the current `Walking → zombie attack` transition's settings (condition on the trigger, no exit time, 0.25s blend).

### 4. Wire transitions out (back to Walking)

Right-click `Zombie Punch 1` → **Make Transition** → drag to `Walking`. Repeat for `Zombie Punch 2` → `Walking`. For each:
- **Condition**: none (leave empty)
- **Has Exit Time**: checked, value `1` (plays the full clip before returning)
- **Transition Duration**: 0.25

This matches the current `zombie attack → Walking` transition.

### 5. Old "Attack" trigger and "zombie attack" state

Decided 2026-08-10: removed, not left in place. `EnemyChaser` no longer fires the `Attack` trigger at all (fully superseded by `Punch1`/`Punch2`), so deleting the `zombie attack` state and the `Attack` parameter is safe — `EnemyAttack.cs`'s internal `Attack` trigger code path only runs if `OnAttackFired` is unassigned, which it never is once `EnemyChaser` subscribes.

### Verify

Enter Play mode, let a CyberSoldier attack you a few times in a row, and confirm the punch animation visibly alternates between the two clips rather than repeating one or freezing on `Walking`. If it doesn't alternate, double check the `Punch1`/`Punch2` parameter names in the Animator exactly match the `EnemyChaser` Inspector fields (case-sensitive).

Once this is wired, revisit `MeleeHitbox_UnitySetup.md` step 3 — the Animation Events (`OnAttackHitFrame`) now need to go on both new punch clips, not the old single Attack clip.
