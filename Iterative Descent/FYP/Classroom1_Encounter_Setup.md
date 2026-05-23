# Classroom 1 - Combat Encounter Setup

## What this sets up
Player walks into Classroom 1 trigger -> pre-placed EnemyChaser enemies activate ->
DDA evaluates when the last one dies. No door changes. Scheduling puzzle door stays as-is.

---

## Step 1 -- Place and configure enemies

1. Place 2-3 EnemyChaser prefab instances inside Classroom 1.
2. Verify each has NavMeshAgent, EnemyChaser, EnemyAttack components.
3. They start dormant (agent.enabled = false) automatically from EnemyBase.Awake().
4. Position them away from the doorway so they are not visible on room entry.

---

## Step 2 -- Create the EncounterTrigger

1. Create a child GameObject on the Classroom 1 entrance (e.g. name it "EncounterTrigger").
2. Add a BoxCollider to it. Set Is Trigger = true.
3. Size the collider to span the full doorway width so the player cannot slip past it.
4. Attach the EncounterTrigger script.
5. In the Inspector, drag each EnemyChaser from the scene into the enemies array.
6. Leave playerTarget empty -- it auto-finds the "Player" tag at Start().

---

## Step 3 -- Verify EnemyDirector TierProfiles

On the GameManager GameObject, check EnemyDirector in the Inspector:
- tierProfiles array must have 5 entries (index 0 to 4).
- If any slot is empty, create TierProfile assets via Right-click -> Create -> DDA -> Tier Profile
  and fill them with the reference values below.

| Index | Tier Name  | Count x | Speed x | Damage x |
|-------|------------|---------|---------|----------|
| 0     | Very Easy  | 0.8     | 0.90    | 0.80     |
| 1     | Easy       | 1.0     | 1.00    | 1.00     |
| 2     | Normal     | 1.2     | 1.05    | 1.05     |
| 3     | Hard       | 1.5     | 1.10    | 1.10     |
| 4     | Very Hard  | 2.0     | 1.20    | 1.15     |

CombatDDAController starts at Tier 1 (Easy) by default.

---

## Step 4 -- Verify CombatDDAController is in the scene

On GameManager, confirm CombatDDAController component is present.
It subscribes to PlayerMetricsTracker.OnEncounterEnd automatically via OnEnable/OnDisable.
No additional wiring needed.

---

## What to verify in the first playtest

1. Walk through the trigger -> Console shows "[EncounterTrigger] Encounter started. Tier 1 | Activated X/X enemies."
2. Enemies begin chasing the player.
3. Kill all enemies -> Console shows "[CombatDDA] Raw: ... | Smoothed: ... | Tier: ..." log.
4. Run a second encounter (reload scene) and check if tier shifted based on first performance.
5. Toggle the DDA HUD overlay with H key to watch the tier badge and score bars live.

---

## DDA scaling with pre-placed enemies (Classroom 1 example with 3 placed)

| Tier | Count multiplier | Enemies activated |
|------|-----------------|-------------------|
| 0    | 0.8             | 2 (floor 3 x 0.8) |
| 1    | 1.0             | 3                 |
| 2+   | 1.2+            | 3 (capped at placed count) |

For later rooms with more enemies placed, higher tiers will activate more.
