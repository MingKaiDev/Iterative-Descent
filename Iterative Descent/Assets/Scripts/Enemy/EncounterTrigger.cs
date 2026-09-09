using System;
using UnityEngine;

/// <summary>
/// Attach to a trigger collider placed at the Classroom 1 entrance.
/// When the player walks in, activates every pre-placed dormant EnemyChaser
/// GameObject in the room.
///
/// Fires once per scene load -- re-entry does not re-trigger.
///
/// ── Scene setup ──────────────────────────────────────────────────────────
///   1. Create a child GameObject on the room entrance with a BoxCollider
///      set to Is Trigger = true.
///   2. Attach this script.
///   3. Assign every pre-placed enemy (EnemyChaser, EnemyRusher, etc.) in the room to the enemies array.
///   4. Leave playerTarget empty -- it is found automatically via the
///      "Player" tag. Assign manually only if you need to override this.
///
/// ── 2026-09 revamp ────────────────────────────────────────────────────────
///   DDA-scaled enemy count has been removed. Every enemy in the enemies array
///   activates every time, regardless of tier. Difficulty now comes entirely
///   from EnemyDirector's per-enemy toughness/aggression multipliers (health,
///   mitigation, speed, damage), applied individually as each enemy activates.
/// </summary>
public class EncounterTrigger : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────

    [Header("Enemies (pre-placed, dormant in scene)")]
    [Tooltip("Any EnemyBase instances placed in this room (EnemyChaser, EnemyRusher, etc). Order matters -- " +
             "enemies are activated front-to-back, though all of them activate regardless of order.")]
    [SerializeField] private EnemyBase[] enemies;

    [Header("Player (leave empty to auto-find by tag)")]
    [Tooltip("The player root Transform. Auto-found via 'Player' tag if left empty.")]
    [SerializeField] private Transform playerTarget;

    [Header("Trigger")]
    [Tooltip("Tag used to identify the player collider entering this trigger.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Checkpoint Rollback")]
    [Tooltip("If a CheckpointTrigger lists this encounter and the player dies and respawns after " +
             "passing it, ResetEncounter() restores every enemy here (dead or mid-fight) back to " +
             "its spawn position/full health so the encounter is refought from scratch. Turn off " +
             "for a one-shot story beat that shouldn't repeat -- e.g. a scripted ambush reveal.")]
    [SerializeField] private bool resetOnCheckpointRespawn = true;

    // ── Events ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired at the end of ResetEncounter(), after every enemy has been restored to spawn.
    /// External bridges that gate their own one-shot logic on this encounter (e.g.
    /// HashTableEnemyEventBridge's _triggered flag) should subscribe so their guard clears
    /// back in sync with the encounter's own reset.
    /// </summary>
    public event Action OnEncounterReset;

    // ── Private ────────────────────────────────────────────────────────────

    private bool _triggered;
    private Vector3[]    _spawnPositions;
    private Quaternion[] _spawnRotations;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    private void Start()
    {
        // Auto-resolve player target if not assigned in Inspector
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindWithTag(playerTag);
            if (player != null)
                playerTarget = player.transform;
            else
                Debug.LogWarning("[EncounterTrigger] Could not find GameObject with tag '" +
                                 playerTag + "'. Assign playerTarget manually in the Inspector.");
        }

        // Snapshot each enemy's original spawn transform (before anything can move them) and
        // flag them Retryable so death doesn't destroy the GameObject -- see EnemyBase.
        // ResetEncounter() below needs both to bring the encounter back for a rematch.
        if (enemies != null)
        {
            _spawnPositions = new Vector3[enemies.Length];
            _spawnRotations = new Quaternion[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null) continue;
                _spawnPositions[i] = enemies[i].transform.position;
                _spawnRotations[i] = enemies[i].transform.rotation;
                enemies[i].Retryable = resetOnCheckpointRespawn;
            }
        }
    }

    // ── Checkpoint Rollback ────────────────────────────────────────────────

    /// <summary>
    /// Called by CheckpointManager (via a CheckpointTrigger's encountersToReset list) when the
    /// player dies and respawns at a checkpoint reached before this encounter. Re-arms the
    /// trigger (walking through it again fires ActivateEncounter() as normal) and restores every
    /// assigned enemy to its spawn position/full health, whether it was dead or still mid-fight.
    /// No-op if resetOnCheckpointRespawn is off for this encounter.
    /// </summary>
    public void ResetEncounter()
    {
        if (!resetOnCheckpointRespawn) return;

        // Only an encounter the player had already reached needs to resume -- one they
        // haven't touched yet is still sitting dormant at spawn and should stay that way
        // until its own trigger/bridge fires normally, not jump the gun before the player
        // ever gets there.
        bool wasEngaged = _triggered;
        _triggered = false;

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null) continue; // despawned some other way -- can't restore
                enemies[i].ResetForRetry(_spawnPositions[i], _spawnRotations[i]);
            }
        }

        Debug.Log($"[EncounterTrigger] Encounter reset for a retry -- " +
                  $"{(enemies != null ? enemies.Length : 0)} enemies restored to spawn.");

        if (wasEngaged)
        {
            // Re-activate immediately instead of waiting for a fresh trigger: a checkpoint can
            // respawn the player back inside/past this encounter's zone, where no fresh
            // OnTriggerEnter will ever fire again, and a headless/bridge-triggered encounter
            // (e.g. the Hash Table Brute, which has no physical collider at all) has no other
            // way back in. Left un-reactivated, every enemy here would stay dormant forever --
            // this is what was behind "NavMesh agent / brute attack doesn't activate after
            // respawn."
            _triggered = true;
            ActivateEnemies();
        }

        OnEncounterReset?.Invoke();
    }

    // ── Trigger ────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        ActivateEncounter();
    }

    // ── Encounter activation ───────────────────────────────────────────────

    /// <summary>
    /// Activates every enemy in this encounter. Guarded by _triggered internally (was
    /// previously guarded in OnTriggerEnter only) so it is now also safe to call directly
    /// from a script that activates this encounter some other way -- e.g. a puzzle-solved
    /// event bridge (see HashTableEnemyEventBridge) -- instead of only ever firing from a
    /// physical trigger collider. That lets those enemies participate in Retryable/
    /// ResetEncounter() checkpoint rollback via this same EncounterTrigger, even though
    /// nothing ever walks through this GameObject's own collider.
    /// </summary>
    public void ActivateEncounter()
    {
        if (_triggered) return;
        _triggered = true;
        ActivateEnemies();
    }

    /// <summary>
    /// Shared activation body used by both a fresh ActivateEncounter() call and
    /// ResetEncounter() resuming an already-engaged encounter after a checkpoint respawn.
    /// Does not touch _triggered -- callers own that.
    /// </summary>
    private void ActivateEnemies()
    {
        if (enemies == null || enemies.Length == 0)
        {
            Debug.LogWarning("[EncounterTrigger] No enemies assigned. Encounter will not start.");
            return;
        }

        if (playerTarget == null)
        {
            Debug.LogWarning("[EncounterTrigger] playerTarget is null. Cannot activate enemies.");
            return;
        }

        // Every pre-placed enemy activates -- DDA no longer withholds enemies by
        // tier (see class doc comment). Per-enemy toughness/aggression scaling
        // still happens inside EnemyBase.Activate() / TakeDamage() individually.
        int activated = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
            {
                Debug.LogWarning($"[EncounterTrigger] enemies[{i}] is null -- skipping.");
                continue;
            }

            // Enemies can start with their GameObject inactive in the scene (e.g. an ambush that
            // should be physically absent from the room, not just AI-dormant, until this fires -- see
            // MeetingRoomKeyProp / RoomReinfestationTrigger). Reactivate before Activate() so its
            // NavMeshAgent is back on the mesh and Activate()'s isOnNavMesh check behaves normally.
            // No-op for the common case where the enemy's GameObject was already active.
            enemies[i].gameObject.SetActive(true);
            enemies[i].Activate(playerTarget);
            activated++;
        }

        // First scripted encounter the player reaches (in any room) shows a one-shot
        // combat control hint. Safe to call on every encounter -- OnboardingHintUI
        // only actually displays it the first time. See Story 31 / OnboardingHintUI.cs.
        if (activated > 0)
            OnboardingHintUI.Instance?.ShowCombatHintOnce();

        int tier = CombatDDAController.Instance != null
            ? CombatDDAController.Instance.CurrentTier
            : -1;

        Debug.Log($"[EncounterTrigger] Encounter (re)started. Tier {tier} | " +
                  $"Activated {activated}/{enemies.Length} enemies.");
    }

    // ── Scene gizmo ────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.2f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.5f,
            $"EncounterTrigger ({(enemies != null ? enemies.Length : 0)} enemies)");
#endif
    }
}
