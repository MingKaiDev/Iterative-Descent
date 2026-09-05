# ARBITEX: Comprehensive Task Breakdown
**As of 2026-09-03 | Final Year Project - AI-Enabled Serious Game**

---

## ✅ COMPLETED & DEPLOYED

### Systems (Fully Implemented)
- **Interactable System** — modular, interface-based; any GameObject can become interactive
- **DDA Metrics System** — 15-value observation vector tracking player performance in real-time
- **Puzzle Systems** — Quiz (MCQ), Computer/Password (auto-type on solve), Linked List (drag-sort)
- **Boss AI (BossAgent v3)** — PPO-trained, deployed to Level 1.unity as DROID_Boss, running in Inference Only mode

### Level Assets (5/14 Rooms Complete)
- **Main Hall** (12m x 8m) — spawn point, south double door, east/west singles
- **Classroom 1** (8m x 16m) — enemy encounter zone, furniture maze, 2 corpses, blood spatter
- **Hallway 1** (4m x 16m segment) — boarded window, collapsed shelf, 1 corpse
- **Washroom** (12m x 8m) — female/male sections, sinks, cubicles, urinals, horror props
- **Stairs 1** (4m x 8m) — scissors staircase, complete with railings, lighting, graffiti, 5 floor levels

### UI & Feedback
- DDA Display HUD (H key toggle) — tier badge, difficulty score, signal breakdown, live metrics

---

## 🚨 CRITICAL PATH — REQUIRED FOR FYP COMPLETION

### AI Director System (Story 14, Education DDA)
**Status:** Designed, architecture ready; needs implementation & training

| Task | Scope | Priority | Est. Time | Notes |
|------|-------|----------|-----------|-------|
| **DirectorAgent.cs (PPO agent)** | Script the agent for puzzle difficulty control; mirror BossAgent pattern | CRITICAL | 2-3 days | Consumes 15-value observation vector; controls next puzzle difficulty tier |
| **Reward function design** | Quiz/linked-list performance → difficulty score mapping | CRITICAL | 1-2 days | Must reflect "optimal difficulty" (not too easy, not too punishing) |
| **Action space definition** | Discrete actions for difficulty tiers (Very Easy → Very Hard) | CRITICAL | 1 day | Likely 5 actions (one per tier) or continuous scaling |
| **PPO training (Director v1)** | Run mlagents-learn for education director; converge on policy | CRITICAL | 3-5 days | Requires 50-100 training episodes; watch for convergence like BossAgent v2 bugs |
| **DifficultyProfile ScriptableObjects** | Asset-side tuning for each difficulty tier (question bank, time limits, etc.) | CRITICAL | 2 days | Makes director outputs actionable in-game |
| **Wire Director to DDA Heuristic** | Hook PPO output → replace current heuristic-only DisplayHUD behavior | CRITICAL | 1 day | No code changes needed (AgentScoreOverride delegate already wired) |

**Dependency:** All 5 tasks block each other; can't train Director without reward function, can't deploy without ScriptableObjects.  
**FYP Impact:** Educational difficulty adaptation is THE core AI deliverable; missing this makes the report incomplete.

---

## 🔨 SECONDARY GAMEPLAY (Nice-to-Have, Gameplay Feel)

### Combat & Enemy System
**Status:** Stubbed (PickupProp exists, nothing else); boss is done but no regular enemies

| Task | Scope | Notes |
|------|-------|-------|
| **EnemyDirector.cs** | Reads CurrentTier → modulates spawn rate, speed, aggression of regular enemies | Already designed in project doc; no implementation yet |
| **Enemy AI scripts** | Basic enemy behavior (EnemyChaser, EnemyRusher already exist but untested) | Navmesh-based pursuit; test against real player, not training dummies |
| **Enemy spawning system** | Wave logic, room-based spawn zones, cooldown between waves | Tier-driven (higher difficulty = more enemies, higher speed) |
| **Combat polish** | Attack animations, hit feedback, knockback, screenshake on impact | Makes combat feel responsive |

**Should this be done?** Yes, for a complete gameplay loop. No, if time is tight — the AI director works without enemies present (puzzles alone can drive DDA).

---

### Enemy Encounters & Progression
| Task | Scope | Notes |
|------|-------|-------|
| **Stalker enemy (PPO-based)** | Stretch goal: train a second RL agent as a roaming hunter | Project doc flags this as v2+ ambition, not required for v1 |
| **Wave scaling by player skill** | EnemyDirector modulates spawn count/speed per room | Depends on DirectorAgent convergence |
| **End-game/escalation** | Boss as final gate; crescendo of difficulty over run | Story-driven by ARBITEX narrative |

---

### Feedback & Polish
| Task | Scope | Notes |
|------|-------|-------|
| **Audio system** | Puzzle hints, ambient loops, boss voice lines (assets don't exist yet) | Cosmetic; can ship without |
| **Stab Attack timeout warning fix** | Add missing OnAttackAnimEnd event in Animation window | Cosmetic; pre-existing, low priority |
| **GroundSlam rebalance** | Shorten cooldown or reduce damage; policy currently camps this attack | Deferred by user 2026-08-29 ("we will work on that soon") — do NOT start without user prompt |
| **Inventory/collectibles** | PickupProp stub needs implementation (ammo, healing, story items) | Scope creep; only if time allows |

---

## ⚠️ MISSING FROM ACTUAL GAME (Not Required for FYP, But Expected in Real Game)

### Core Gameplay Loop
| Missing | Impact | Why Not Included |
|---------|--------|------------------|
| **Win/Lose Conditions** | No explicit end-state; player can roam indefinitely | FYP is proof-of-concept; narrative end is ARES escape |
| **Checkpoint/Save System** | Death reloads entire scene; no progress persistence mid-run | Out of scope for AI-focused project |
| **Inventory system** | No way to collect/store/use items | Collectibles are stubbed, not implemented |
| **Permanent upgrades/progression** | No meta-progression (unlocks, permanent stats) | Out of scope |

### UI/Menus
| Missing | Impact |
|---------|--------|
| **Main Menu** | Game starts directly in Level 1 |
| **Pause Menu** | No in-game pause option |
| **Settings/Options** | No audio/graphics/difficulty sliders |
| **End-of-Run Summary** | No debriefing screen showing metrics, time played, etc. |
| **Tutorial/Onboarding** | No guided intro; player must figure out controls |

### Technical
| Missing | Impact |
|---------|--------|
| **Level 1 → Courtyard Transition** | Deliberately broken so ARES stays unreachable in pilot build (Story 32 decision) |
| **Narrative/Cutscenes** | No voice lines, cinematics, or story progression system |
| **Graphics Polish** | Placeholder textures, no custom shaders, minimal VFX | Explicitly secondary per project brief |

---

## 🎮 GAMEPLAY LOOP GAPS (Current Experience Breakdown)

### What Works Now
1. **Exploration** — walk around rooms, encounter interactable objects (mostly puzzles)
2. **Puzzle solving** — MCQ quiz, password entry, linked-list sorting; all give feedback and unlock doors
3. **Boss fight** — encounter DROID_Boss, watch it use trained PPO policy; can die and restart
4. **DDA observation** — hit H key to see real-time difficulty metrics (for dev/debug only)

### What's Missing (Breaks Immersion)
| Gap | Current Behavior | Expected Behavior |
|-----|------------------|-------------------|
| **No regular enemies between puzzles** | Walk freely between rooms with zero threat | Constant ambient danger; player must balance speed vs. caution |
| **No combat outside boss arena** | Encounters are binary (puzzle solve or boss) | Ongoing threat-based difficulty; combat shapes puzzle time decisions |
| **No fail-forward loops** | Death = full scene reload; no "easier" retries | Tier system should adapt after failure; player should see DDA responding to them |
| **No meta-loop** | Single run only; no "next run" with learned settings | In a real serious game: player learns optimal strategies; game learns optimal player model |
| **No story integration** | Puzzles exist in vacuum; no "why are you solving these?" | ARBITEX narrative should contextualize each puzzle as training/testing |
| **No collectibles/resources** | Pickups are stubbed; nothing to find or manage | Resource scarcity drives player choices (use healing now or save for boss?) |
| **No visible progression** | Only internal metric is DDA tier; player doesn't see themselves improving | HP bar, score, completion %, skill unlock visual feedback |

---

## 📋 RANKED BY FYP IMPORTANCE

### Tier 1: Report/Thesis Must-Haves (Required to Pass)
1. ✅ **Boss AI deployed and tested** (DONE — BossAgent v3)
2. 🚨 **Education Director AI implemented, trained, and ablated** (Director v1)
3. ✅ **DDA metrics system logging real gameplay data** (DONE — observation vector complete)
4. ✅ **Playable game proving the AI works in context** (DONE — Level 1.unity boots and plays)

### Tier 2: Report Enhancement (Strengthens Thesis)
1. 🔨 **Enemy spawning system responds to EnemyDirector tier** (Optional but shows DDA breadth)
2. 🔨 **Quantitative comparison: Fixed vs. Adaptive difficulty** (Requires two test runs; makes for strong results section)
3. ⚠️ **Narrative framing of puzzles as ARBITEX trials** (Minimal; just text overlays, but ties story to mechanics)

### Tier 3: Polish (Nice for Demo/Presentation)
1. ⚠️ **Main menu + pause + settings** (Looks more polished but doesn't affect AI evaluation)
2. ⚠️ **Audio design** (Atmospheric but not critical)
3. ⚠️ **Graphics/VFX upgrades** (Explicitly deprioritized per project scope)

---

## 🎯 RECOMMENDED PRIORITY ORDER (Next Steps)

### Phase 1: Get Director AI Working (2–3 weeks)
- [ ] Write DirectorAgent.cs (2–3 days)
- [ ] Design reward function for "optimal difficulty" (1–2 days)
- [ ] Build DifficultyProfile ScriptableObjects with tuning curves (2 days)
- [ ] Train Director v1 (3–5 days of iteration)
- [ ] Validate: Does player performance actually correlate to difficulty changes? (1 day of playtesting)

### Phase 2: Light Enemy Support (1–2 weeks, optional)
- [ ] Implement EnemyDirector.cs reading Director's tier (1 day)
- [ ] Spawn 2–3 simple enemies per room at scaling difficulties (3 days)
- [ ] Playtest combat feels responsive enough (1–2 days)
- [ ] **Skip:** complex enemy AI, stalker RL agent, full enemy roster

### Phase 3: Quantitative Evaluation (1 week)
- [ ] Run 10 sessions with fixed difficulty; record metrics (2 days)
- [ ] Run 10 sessions with adaptive (Director-driven) difficulty; record metrics (2 days)
- [ ] Compare: engagement, performance variance, player feedback (1–2 days)
- [ ] Write up findings for thesis

### Phase 4: Polish & Report (Remaining time)
- [ ] Add main menu / basic UI if time allows
- [ ] Write FYP thesis chapters
- [ ] Compress deliverables (game build + code + thesis PDF)

---

## 🚫 EXPLICITLY OUT OF SCOPE (Don't Start These)

- **Full AAA enemy roster** — use 2–3 simple types max
- **Custom graphics/shaders** — project doc explicitly says no
- **Procedural dungeon generation** — fixed level layout is fine
- **Online multiplayer** — not serious-game relevant
- **Complex crafting/inventory** — PickupProp stub is enough proof-of-concept
- **GroundSlam rebalance** — deferred by user 2026-08-29; do NOT touch without explicit request

---

## 🔗 Key Dependencies

```
DirectorAgent v1 (training)
    ├─ requires: Reward function design ✋
    ├─ requires: DifficultyProfile ScriptableObjects ✋
    └─ enables: EnemyDirector spawn scaling (optional)
    
BossAgent v3 (already live)
    ├─ deployed to Level 1.unity ✅
    ├─ known issue: GroundSlam spam (deferred) 🎯
    └─ requires: real player playtesting feedback
    
Remaining Rooms (Blender assets)
    ├─ Computer Lab, Classroom 2-3, Hallway 2, General Office, Stairs 2-3, Courtyard, Shack
    ├─ depends on: visual pipeline finalization (texture style, prop set, lighting mood)
    └─ medium priority: demo doesn't require all 14 rooms; 5 existing rooms + boss sufficient for core loop proof
```

---

## ⏱️ Effort Estimates (Person-Days)

| Component | Dev | Test | Polish | Total |
|-----------|-----|------|--------|-------|
| DirectorAgent + training | 5 | 2 | 1 | **8 days** |
| DifficultyProfiles tuning | 1 | 1 | 1 | **3 days** |
| EnemyDirector + spawning (optional) | 2 | 1 | 1 | **4 days** |
| Main Menu / UI (optional) | 2 | 1 | 1 | **4 days** |
| Remaining rooms (9 Blender assets) | 9 | 1 | 2 | **12 days** |
| Audio implementation (optional) | 2 | 1 | 1 | **4 days** |
| Thesis writing & playtest eval | 10 | N/A | 2 | **12 days** |
| **CRITICAL PATH TOTAL** | — | — | — | **~11 days** |
| **Full feature set** | — | — | — | **~45 days** |

**Note:** Critical path (DirectorAgent + evaluation + thesis) is ~11 days if focused; full release-quality game would take ~45 days with current team.

---

## 📌 Known Deferred Issues (Do Not Restart)

| Issue | Status | User Decision |
|-------|--------|-----------------|
| GroundSlam dominance in boss AI | Found, root cause: symmetric cooldown balance | Deferred 2026-08-29: "we will work on that soon" — do NOT change without user prompt |
| Stab Attack timeout warning | Cosmetic; missing Animation Event | Deferred; low priority; cosmetic |
| Level 1 → Courtyard transition | Deliberately broken (Story 32 decision) | Keep disabled until ARES encounter is meant to open |

---

## ✨ Bottom Line

**What MUST be done for FYP:** DirectorAgent v1, trained & validated (Tier 1).  
**What SHOULD be done for a playable game:** EnemyDirector + enemy spawning + 9 remaining rooms (Tier 2 + 3).  
**What CAN be skipped:** Audio, graphics polish, extra menus, complex enemy AI (Tier 3 + "Out of Scope").

**Current state:** Boss AI is live and running; educational director AI is designed but not implemented. Next blocker is DirectorAgent development.
