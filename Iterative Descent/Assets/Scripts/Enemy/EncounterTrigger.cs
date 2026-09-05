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

    // ── Private ────────────────────────────────────────────────────────────

    private bool _triggered;

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
    }

    // ── Trigger ────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag(playerTag)) return;

        _triggered = true;
        ActivateEncounter();
    }

    // ── Encounter activation ───────────────────────────────────────────────

    private void ActivateEncounter()
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

        Debug.Log($"[EncounterTrigger] Encounter started. Tier {tier} | " +
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
