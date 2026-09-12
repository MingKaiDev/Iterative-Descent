// AresAttackDisableMachineProp.cs
// One machine, one attack. Place X of these (one per entry in AresAttack below) wherever
// the reward room is (project-level2-library.md, 7-step flow, step 7). Interacting with
// any one permanently disables that attack (via AresAttackDisableManager) and locks every
// other machine in the world out of interaction -- the player can only ever pick one.
//
// Setup, per machine:
//   1. Add InteractableBase   (outline, prompt, radius)
//   2. Add InteractableRegistrar  (auto-registers into PlayerInteractor)
//   3. Add this script
//   4. Set attackToDisable to the one attack THIS machine disables -- give every machine
//      in the room a different value, one each, so all 8 of ARES's attacks are covered
//      exactly once. Same roster as BossAgent.cs's "attacks[8]" Inspector array (Slash,
//      LeftPunch, Stab, BladeSweep, GroundSlam, Pounce, SprintCharge, CoreOverload) --
//      listed here as an enum only so this script doesn't need a direct reference to
//      ARES's own GameObject to pick one, not because array order matters here (see
//      AresAttackDisableManager's doc comment for why type identity is used instead of
//      index).
//   5. Make sure exactly one AresAttackDisableManager exists somewhere persistent in the
//      game (see that script's own setup instructions) -- every machine finds it via
//      Instance at Interact() time.
//   6. Add ExitDoor As Well
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class AresAttackDisableMachineProp : MonoBehaviour, IInteractable
{
    public enum AresAttack
    {
        Slash, LeftPunch, Stab, BladeSweep, GroundSlam, Pounce, SprintCharge, CoreOverload
    }

    [Header("Assignment")]
    [Tooltip("Which of ARES's attacks this specific machine permanently disables. Give every machine in the room a different value.")]
    public AresAttack attackToDisable;

    [Header("Door To Unlock")]
    [Tooltip("The DoorController on the Level 2 exit door. Unlocked (and opened, via " +
         "DoorController.Unlock()) the instant this key is picked up.")]
    public DoorController exitDoor;

    [Header("Dialogue (optional)")]
    [Tooltip("Plays via DialogueManager.Interrupt() the instant exitDoor unlocks. Leave empty to skip.")]
    public DialogueSequence unlockDialogue;

    public string InteractLabel => $"Permanently Disable ARES's {attackToDisable}";

    void OnEnable()
    {
        AresAttackDisableManager.OnAttackDisabled += HandleAnyAttackDisabled;

        // Covers the player leaving and re-entering the room after another machine
        // already fired earlier this session -- lock out immediately instead of waiting
        // for a broadcast that already happened before this object was (re-)enabled.
        if (AresAttackDisableManager.Instance != null && AresAttackDisableManager.Instance.HasDisabledAnAttack)
            LockOut();
    }

    void OnDisable()
    {
        AresAttackDisableManager.OnAttackDisabled -= HandleAnyAttackDisabled;
    }

    public void Interact(GameObject interactor)
    {
        if (AresAttackDisableManager.Instance == null)
        {
            Debug.LogWarning("[AresAttackDisableMachineProp] No AresAttackDisableManager found -- nothing disabled.", this);
            return;
        }

        AresAttackDisableManager.Instance.TryDisableAttack(ResolveType(attackToDisable));
        // Every machine, including this one, locks itself out via the OnAttackDisabled
        // broadcast above -- no need to call LockOut() directly here too.
        if (exitDoor != null)
        {
            exitDoor.Unlock();

            if (unlockDialogue != null)
            {
                if (DialogueManager.Instance != null)
                    DialogueManager.Instance.Interrupt(unlockDialogue);
                else
                    Debug.LogWarning("[LevelExitKeyProp] DialogueManager.Instance is null -- unlockDialogue will not play.", this);
            }
        }
        else
        {
            Debug.LogWarning("[LevelExitKeyProp] No exitDoor assigned.", this);
        }
    }

    void HandleAnyAttackDisabled(System.Type disabledType) => LockOut();

    void LockOut()
    {
        GetComponent<InteractableBase>().enabled = false;
        GetComponent<InteractableRegistrar>().enabled = false;
        enabled = false;
    }

    static System.Type ResolveType(AresAttack attack)
    {
        switch (attack)
        {
            case AresAttack.Slash:        return typeof(SlashAttack);
            case AresAttack.LeftPunch:    return typeof(LeftPunchAttack);
            case AresAttack.Stab:         return typeof(StabAttack);
            case AresAttack.BladeSweep:   return typeof(BladeSweepAttack);
            case AresAttack.GroundSlam:   return typeof(GroundSlamAttack);
            case AresAttack.Pounce:       return typeof(PounceAttack);
            case AresAttack.SprintCharge: return typeof(SprintChargeAttack);
            case AresAttack.CoreOverload: return typeof(CoreOverloadAttack);
            default:                      return null;
        }
    }
}
