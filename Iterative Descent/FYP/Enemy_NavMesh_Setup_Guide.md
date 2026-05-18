# Enemy NavMesh + Chase System — Setup Guide

## What was built

| File | Purpose |
|------|---------|
| `Scripts/Enemy/IEnemy.cs` | Interface: `Activate`, `Deactivate`, `Die`, `IsDead` |
| `Scripts/Enemy/EnemyBase.cs` | Abstract MonoBehaviour — health, death, NavMeshAgent ref |
| `Scripts/Enemy/EnemyChaser.cs` | Concrete enemy: state machine + NavMesh chasing |
| `Scripts/Enemy/EnemyAttack.cs` | Periodic melee attack with animation event stubs |
| `Scripts/Enemy/FistHitboxRelay.cs` | Sits on the fist bone — relays `OnTriggerEnter` to EnemyAttack |
| `Scripts/Events/EnemyEventBridge.cs` | Listens to `OnLinkedListSolved`, delays, then calls `enemy.Activate()` |

`IDamageable.cs` already existed in `Scripts/Player/` — no changes made to it.

---

## Phase 1 — NavMesh Bake (Editor)

### 1.1 Confirm the package is installed
Window → Package Manager → search "AI Navigation" → it should show **com.unity.ai.navigation** as installed. ✓ (already in your project)

### 1.2 Bake the NavMesh surface
1. Select your **floor/level root GameObject** (the object that holds all walkable geometry).
2. Add Component → **NavMeshSurface**.
3. In the NavMeshSurface Inspector:
   - **Agent Type**: Humanoid (or whatever you name your agent type)
   - **Collect Objects**: All Game Objects in the scene (or Children — your call)
   - **Include Layers**: set to only geometry layers (exclude UI, Trigger, Enemy, etc.)
4. Click **Bake** at the bottom of the component.
5. Blue overlay appears on walkable surfaces — that's your nav mesh. ✓

> **Tip**: Re-bake any time you move static geometry. The bake is baked into the scene file, so commit it to Git after baking.

### 1.3 Mark non-walkable obstacles
For furniture, desks, walls — add a **NavMeshObstacle** component if they are *not* part of the baked mesh.
- **Carve**: ☑ (digs a hole in the NavMesh so agents walk around it)
- Set **Shape** to Box and size it to match the object.

---

## Phase 2 — Enemy GameObject Setup

### 2.1 Build the enemy prefab hierarchy

```
EnemyRoot  ← EnemyChaser + EnemyAttack + NavMeshAgent + Capsule/Mesh
└── CharacterRig  ← Animator (your imported FBX rig)
    └── ... → Hand_R_Bone
        └── FistHitbox  ← BoxCollider (IsTrigger ☑) + FistHitboxRelay
```

### 2.2 NavMeshAgent component (on EnemyRoot)

| Field | Value |
|-------|-------|
| Agent Type | Humanoid |
| Base Offset | match to character height (usually 0) |
| Speed | leave at default — EnemyChaser overrides via `chaseSpeed` |
| Stopping Distance | leave at 0 — EnemyChaser sets this to `attackRange * 0.85` |
| Obstacle Avoidance Radius | 0.5 (approximate body width / 2) |
| Obstacle Avoidance Quality | Medium |

### 2.3 EnemyChaser component (on EnemyRoot)

| Field | Set to |
|-------|--------|
| Max Health | 100 (EnemyBase field) |
| Chase Speed | 4 |
| Attack Range | 2 |
| Path Update Rate | 0.2 |
| Speed Param Name | `Speed` (must match Animator float parameter name) |
| Dead Param Name | `IsDead` (must match Animator bool parameter name) |

### 2.4 EnemyAttack component (on EnemyRoot)

| Field | Set to |
|-------|--------|
| Attack Cooldown | 1.8 |
| Melee Damage | 20 |
| Hitbox Active Window | 0.12 |
| Fist Hitbox | drag the FistHitbox child collider here |
| Animator | leave empty — auto-found in children |

### 2.5 FistHitbox child GameObject (on the fist bone)

1. In the imported FBX rig, find your right-hand bone (e.g. `mixamorig:RightHand`).
2. Right-click → **Create Empty Child** → name it `FistHitbox`.
3. Add **BoxCollider** → enable **Is Trigger** ☑ → size to cover fist.
4. Add **FistHitboxRelay** → `parentAttack` auto-finds `EnemyAttack` in parents.
5. Drag this `FistHitbox` collider into `EnemyAttack.fistHitbox` field.

> The collider starts **disabled** — EnemyAttack enables it only during the
> swing window when `OnAttackHitFrame()` fires.

---

## Phase 3 — Animator Setup

Create an Animator Controller and assign it to the character rig's **Animator** component.

### Parameters to add
| Name | Type | Used by |
|------|------|---------|
| `Speed` | Float | EnemyChaser — blend tree for walk/run |
| `Attack` | Trigger | EnemyAttack — fires the swing |
| `IsDead` | Bool | EnemyChaser — transition to death state |

### Suggested state layout
```
[Any State] ──IsDead==true──► Death
[Idle/Walk Blend Tree] ──Attack trigger──► Attack
[Attack] ──(exit time)──► Idle/Walk Blend Tree
```

### Animation Events to add (on the Attack clip)
1. Open the **Attack** animation clip in the Animation window.
2. Drag the scrubber to the **impact frame** (the frame the fist makes contact).
   - Add Event → Function: `OnAttackHitFrame`
3. Drag the scrubber to the **very last frame** of the clip.
   - Add Event → Function: `OnAttackEnd`

> These functions live on `EnemyAttack.cs`, which is on the **root** of the enemy — Unity's Animation Event system calls functions on Animator's root GameObject, so this works correctly.

---

## Phase 4 — Scene Wiring

### 4.1 EnemyEventBridge
1. Select your **persistent GameManager** (the one that has `LinkedListEventHandler`).
2. Add Component → **EnemyEventBridge**.
3. Assign:
   - **Enemy**: drag the `EnemyRoot` object (or its prefab instance) from the Classroom.
   - **Player Transform**: drag the Player root GameObject.
   - **Activation Delay**: `2.0` (door animates for 1s, add 1s of suspense).

### 4.2 Enemy start state
The enemy should be **placed in the scene but inactive** (inside or just behind the classroom door). `EnemyBase.Awake()` already sets `enabled = false` and `agent.isStopped = true`, so it will sit dormant until `EnemyEventBridge` calls `Activate()`.

### Event flow at runtime
```
Player solves LinkedList puzzle
  │
  ├── LinkedListPuzzleUI.OnLinkedListSolved fires
  │
  ├── LinkedListEventHandler → door.Unlock() → door opens (1s animation)
  │
  └── EnemyEventBridge waits 2s → calls enemy.Activate(playerTransform)
        → EnemyChaser state: Idle → Chasing
        → NavMeshAgent.isStopped = false
        → SetDestination(player) every 0.2s
        → When dist ≤ attackRange: state → Attacking
        → EnemyAttack.TryAttack() fires "Attack" trigger
        → Animation event OnAttackHitFrame() enables fist collider
        → FistHitboxRelay.OnTriggerEnter → EnemyAttack.OnFistHit
        → IDamageable.TakeDamage(20, hitPoint) on player
```

---

## Phase 5 — Killing the Enemy

Both ranged and melee damage paths work the same way: call `TakeDamage(amount, hitPoint)` on the enemy.

Since `EnemyChaser : EnemyBase : IDamageable`, you can get the component as either:
```csharp
// From a bullet/projectile hit:
IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
target?.TakeDamage(bulletDamage, hit.point);

// Or direct reference if you have it:
enemyChaser.TakeDamage(meleeDamage, hitPoint);
```

`EnemyBase.TakeDamage()` subtracts health → at 0, calls `Die()` → `OnDie()` sets `IsDead = true`, sets `IsDead` animator bool, destroys after 3s.

---

## Stub checklist — to finish later

- [ ] Replace `OnHit` debug log with hit-stagger animation trigger
- [ ] Replace `OnDie` Destroy with ragdoll or death animation
- [ ] `EnemyEventBridge`: replace stub log with real SFX / camera shake
- [ ] Wire `EnemyAttack.meleeDamage` into player health system (PlayerCombat / PlayerHealth)
- [ ] DDA integration: `EnemyChaser.chaseSpeed` driven by `DDAController.CurrentTier`
- [ ] Object pooling for enemies once multiple rooms spawn them

---

## Adding a second enemy type

1. Create `class TurretEnemy : EnemyBase` (or whatever type you want).
2. Override `OnActivate`, `OnDeactivate`, `OnDie`, `OnHit`.
3. Write your own `Update()` — no NavMeshAgent chasing required if it's stationary.
4. Optionally skip `EnemyAttack` and write your own projectile firing logic.
5. The rest of the system (IDamageable, IEnemy, EnemyEventBridge) works unchanged.
