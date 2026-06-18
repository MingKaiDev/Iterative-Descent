# Shotgun Setup Guide

## What was implemented

| File | Change |
|---|---|
| `Assets/Scripts/Player/ShotgunPellet.cs` | New - projectile that deals damage on collision |
| `Assets/Scripts/Player/ShotgunController.cs` | New - weapon script, mirrors PlayerCombat pattern |
| `Assets/Scripts/Props/ShotgunPickupProp.cs` | New - IInteractable floor pickup, press E to equip |
| `Assets/Scripts/Player/PlayerMovement.cs` | Updated - IsAiming / CancelAim checks both weapons |
| `Assets/Scripts/Player/PlayerPropScript.cs` | Updated - pistol transform accounts for shotgun aim state |
| `Assets/Scripts/UI/CombatHUD.cs` | Updated - subscribes to ShotgunController.OnAmmoChanged |

Weapon swap logic: pistol (PlayerCombat) disabled, shotgun (ShotgunController) enabled. Only one
fires at a time. The HUD ammo counter automatically shows whichever weapon is active.

---

## Step 1: Create the Pellet Prefab

1. In the Hierarchy, create: GameObject > 3D Object > Sphere. Rename it "ShotgunPellet".
2. Scale it to (0.08, 0.08, 0.08).
3. On the Sphere Collider, set Radius to 0.04. Tick "Is Trigger" OFF (it needs OnCollisionEnter).
4. Add Component > Rigidbody. Leave defaults; the script sets useGravity=false at runtime.
5. Add Component > ShotgunPellet.
6. Create a new Physics Material called "Pellet" (no friction/bounce) and assign it to the Sphere Collider.
7. Drag into Assets/Prefab/ and call it "ShotgunPellet". Delete the scene instance.

### Physics Layer Matrix (required to prevent self-hit)

1. Edit > Project Settings > Tags and Layers. Add a new layer called "Pellet" (e.g., Layer 9).
2. Edit > Project Settings > Physics. In the Layer Collision Matrix:
   - Uncheck "Pellet x Player" so pellets never collide with the player.
3. Select your ShotgunPellet prefab and set its Layer to "Pellet".

---

## Step 2: Add ShotgunController to the Player

1. Select the Player GameObject in the Hierarchy.
2. Add Component > ShotgunController.
3. In the Inspector, untick the checkbox to the left of "ShotgunController" (disable it -- shotgun not
   equipped at start).
4. Assign fields:
   - Pellet Prefab: the ShotgunPellet prefab from Step 1.
   - Bullet Impact Prefab: same impact prefab you use for the pistol (optional).
   - Muzzle Flash: same or new muzzle ParticleSystem (optional).
   - Pistol Model: drag the Glock17 GameObject from the Hierarchy.
   - Shotgun Model: see Step 3 below.

---

## Step 3: Add the Shotgun Model to the Player

1. Import your shotgun FBX into Assets/Art/Props/.
2. Drag it into the scene as a child of the "mixamorig:RightHand" bone (same bone the pistol uses).
3. Zero out Position. Tune Rotation and Scale so it sits correctly in the hand (refer to PlayerPropScript
   idleLocalPosition/Rotation for the pistol as a reference).
4. With the shotgun model visible alongside the pistol, save your tuned transform values to
   ShotgunController's "shotgunIdleLocalPosition" if you add those fields later.
5. Disable the shotgun model GameObject in the Hierarchy -- ShotgunController.Awake() hides it anyway,
   but disabling avoids a one-frame flash.
6. Back on ShotgunController in the Player Inspector, assign "Shotgun Model" to this GameObject.

---

## Step 4: Create the Pickup Prop in the Scene

1. Drag your shotgun FBX into the scene at the pickup location (floor of classroom, etc.).
2. Add a BoxCollider (make it slightly larger than the mesh for easier interaction).
3. Add Component > InteractableBase.
4. Add Component > InteractableRegistrar.
5. Add Component > ShotgunPickupProp.
   - Starting Mag: 2 (player gets 2 shells ready in the magazine).
   - Starting Spare: 12 (spare shells in reserve).
6. Optionally assign the quick-outline material in InteractableBase (white/yellow highlight).

The player walks up to the prop, presses E, and equips the shotgun.

---

## Step 5: Test Part by Part

### Test A -- Pellet fires correctly
1. Temporarily enable ShotgunController on the Player in the Inspector.
2. In ShotgunController, set Starting Mag = 6 (it reads _currentMag on enable, but for testing set
   via EquipWithAmmo in a test script or just verify BroadcastAmmo fires).
3. Play. Right-click to aim, left-click to shoot. Verify 6 pellets spawn in a spread cone.
4. Check Console for "pelletPrefab is not assigned" warnings if nothing fires.
5. Disable ShotgunController on the Player again when done testing.

### Test B -- Pickup works
1. Place the pickup prop in the scene (Step 4).
2. Play. Walk up to the prop, press E.
3. Expected: prompt shows "Pick up Shotgun", pistol model disappears, shotgun model appears,
   ammo HUD shows [2 | 12], pickup prop removed from scene.
4. Right-click to aim, left-click to fire.

### Test C -- Weapon swap integrity
1. After picking up the shotgun, verify:
   - Sprint (Shift) while aiming cancels aim.
   - R reloads from spare pool.
   - Empty mag triggers dry-fire (no crash).
   - Death screen still appears on player death.

---

## Known Limitations / Next Steps

- The character plays pistol rig animations (Pistol Idle, Pistol Walk, Shoot, Reload) with the
  shotgun mesh visible. Add dedicated shotgun animator states when animations are available.
- No switching back to pistol once shotgun is picked up (by design -- can add swap key later).
- Shotgun pellets use the same impact prefab as pistol bullets. Add a separate shotgun impact VFX
  (wider scatter decal) for polish.
- ShotgunAmmoPickup.cs (shell box pickup) not yet implemented -- reuse the AmmoPickup.cs pattern
  but call ShotgunController.AddAmmo() instead of PlayerCombat.AddAmmo().
