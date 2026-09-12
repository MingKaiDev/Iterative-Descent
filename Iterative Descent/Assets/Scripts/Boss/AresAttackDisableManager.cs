// AresAttackDisableManager.cs
// Persistent singleton backing the "library loop" reward (project-level2-library.md,
// step 7): X machines in the world, each permanently disabling one of ARES's attacks.
// Interacting with any one machine locks out every other machine for the rest of the
// playthrough -- the player can only ever disable exactly one attack, never all of them.
//
// Identifies an attack by its concrete BossAttackBase subclass (SlashAttack,
// GroundSlamAttack, etc.) rather than by array index. BossAttackRegistry's own _attacks
// array (Unity component order via GetComponents<BossAttackBase>()) and BossAgent's
// attacks[8] array (an explicit, hand-wired Inspector order -- see that file's "FIXED
// ORDER" comment) are two independently-ordered arrays over the same 8 components, so an
// index chosen in one has no guaranteed meaning in the other. Type identity sidesteps
// that mismatch entirely and needs no index bookkeeping here.
//
// Affects BOTH systems that can pick ARES's attacks, per Ming Kai's direction
// (2026-09-11): BossAttackRegistry.TryExecuteRandomAttack() (the dumb-mode fallback) and
// BossAgent.WriteDiscreteActionMask() (the trained PPO policy currently deployed on Level
// 1.unity's real boss, smart mode). Masking a PPO agent's action space at inference time
// is safe and needs no retraining -- WriteDiscreteActionMask() already masks on
// cooldown/variety-cooldown/range every decision, this is one more condition alongside
// those, not a new mechanism.
//
// Setup:
//   1. Attach to a persistent GameObject in whichever scene the player reaches the
//      machines in (e.g. the same GameManager PlayerMetricsTracker lives on). Awake()
//      below calls DontDestroyOnLoad(gameObject) itself -- do NOT skip this or rely on
//      the GameObject already being persistent for another reason. project-bst-catalog-
//      puzzle.md documents exactly this bug once already (PuzzleDDAController living on a
//      "Game Manager" object that was never marked DontDestroyOnLoad, silently losing all
//      its state on the very next scene load) -- this script owns its own persistence so
//      it can't repeat that mistake regardless of what else is on the object it's dropped
//      onto.
//   2. Nothing else to wire here -- AresAttackDisableMachineProp (Assets/Scripts/Props/)
//      finds this via Instance, and BossAttackRegistry/BossAgent do the same.
using System;
using System.Collections.Generic;
using UnityEngine;

public class AresAttackDisableManager : MonoBehaviour
{
    public static AresAttackDisableManager Instance { get; private set; }

    /// <summary>Fired once, the moment any machine successfully disables an attack --
    /// every AresAttackDisableMachineProp in the world (including the one just used)
    /// subscribes to this to lock itself out. Never fires a second time in a given
    /// playthrough, since TryDisableAttack() below is itself one-shot.</summary>
    public static event Action<Type> OnAttackDisabled;

    private readonly HashSet<Type> _disabledAttackTypes = new HashSet<Type>();
    private bool _hasDisabledAnAttack;

    /// <summary>True the instant any one machine has been used. Machines check this on
    /// their own OnEnable() (not just the OnAttackDisabled event) so a machine that
    /// enters the world AFTER the choice was already made -- e.g. the player leaving and
    /// re-entering the room -- still starts locked out instead of waiting for a broadcast
    /// that already happened.</summary>
    public bool HasDisabledAnAttack => _hasDisabledAnAttack;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[AresAttackDisableManager] Duplicate instance found -- destroying the new one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>True if 'attack' is the permanently-disabled one. Safe to call with null
    /// (returns false) -- BossAttackRegistry/BossAgent both already null-check their own
    /// attack slots before this, but this stays defensive on its own.</summary>
    public bool IsDisabled(BossAttackBase attack)
    {
        return attack != null && _disabledAttackTypes.Contains(attack.GetType());
    }

    /// <summary>Called by AresAttackDisableMachineProp.Interact(). Returns false (no-op)
    /// if an attack has already been disabled this playthrough -- the one-shot lock that
    /// makes "disable exactly one, never all of them" hold even if a wiring mistake ever
    /// left more than one machine interactable at once.</summary>
    public bool TryDisableAttack(Type attackType)
    {
        if (_hasDisabledAnAttack)
        {
            Debug.Log("[AresAttackDisableManager] An attack is already permanently disabled -- ignored.", this);
            return false;
        }

        if (attackType == null)
        {
            Debug.LogWarning("[AresAttackDisableManager] TryDisableAttack called with a null type -- ignored.", this);
            return false;
        }

        _disabledAttackTypes.Add(attackType);
        _hasDisabledAnAttack = true;

        Debug.Log($"[AresAttackDisableManager] Permanently disabled ARES's {attackType.Name}.", this);
        OnAttackDisabled?.Invoke(attackType);
        return true;
    }
}
