# Iterative-Descent

**Adaptive Difficulty Through an Intelligent Game Director** — a Unity 3D horror
serious game where every system, from puzzles to boss fights, adapts to how the
player is actually performing.

## Overview
Set in a facility run by a rogue AI warden, ARBITEX, the game pairs full FPS
horror gameplay with two independently-driven Dynamic Difficulty Adjustment
(DDA) systems — one for educational puzzles, one for combat — so that neither
skill domain distorts the other's difficulty curve.

## Key Features
- **Puzzle AI** — Bayesian Knowledge Tracing tracks per-concept mastery across
  9 CS-concept puzzles (linked lists, hash tables, graph search, subnetting,
  scheduling, and more); a heuristic EMA director blends BKT state with raw
  performance into a 5-tier difficulty score
- **Combat AI** — a separate heuristic EMA controller scores accuracy, health,
  pacing, and ammo economy per encounter to retune enemy count, speed, and
  damage
- **Boss AI** — main boss (ARES) driven by a PPO agent trained with Unity
  ML-Agents (16-float observation space, 9-action masked discrete policy,
  custom reward shaping); second boss (Deimos) runs a hand-authored 8-move
  heuristic state machine for direct comparison
- **Full game loop** — FPS combat with multiple weapons, checkpoint/respawn,
  branching dialogue, audio, locked-door/key progression, and a modular
  Blender-to-FBX-to-Unity level pipeline

## Tech Stack
Unity 3D · C# · Unity ML-Agents (PPO) · Blender · TextMeshPro · Unity New
Input System

## Status
Core game loop, puzzle suite, and both DDA systems are complete and
playtested; deployed as a WebGL build. Built as a Final Year Project.
