using UnityEngine;

/// <summary>
/// Plays a DialogueSequence when the player enters the trigger collider.
/// Requires a Collider set to Is Trigger on this GameObject.
///
/// Also exposes PlayDialogue() as a public method so it can be called from
/// any other script (e.g. EnemyDirector, PuzzleEventHandler, DoorController)
/// without needing a trigger zone.
///
/// Example usage from code:
///   GetComponent[DialogueTrigger]().PlayDialogue();
///
///   -- or directly via the manager --
///   DialogueManager.Instance.Enqueue(mySequence);
/// </summary>
[RequireComponent(typeof(Collider))]
public class DialogueTrigger : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Dialogue")]
    [Tooltip("The sequence to play when triggered.")]
    [SerializeField] private DialogueSequence _sequence;

    [Header("Behaviour")]
    [Tooltip("If true the trigger fires only once. Recommended for story beats.")]
    [SerializeField] private bool _playOnce = true;

    [Tooltip("If true the new sequence cuts off whatever is currently playing and clears the queue. " +
             "If false the sequence is added to the back of the queue.")]
    [SerializeField] private bool _interruptCurrent = false;

    [Tooltip("Tag of the GameObject that activates this trigger. Leave blank to accept any tag.")]
    [SerializeField] private string _requiredTag = "Player";

    // ─── State ───────────────────────────────────────────────────────────────────

    private bool _fired = false;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_playOnce && _fired) return;

        if (!string.IsNullOrEmpty(_requiredTag) && !other.CompareTag(_requiredTag)) return;

        PlayDialogue();
    }

    // ─── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Manually trigger the dialogue sequence from code.
    /// Respects the playOnce and interruptCurrent settings.
    /// </summary>
    public void PlayDialogue()
    {
        if (_playOnce && _fired) return;
        if (_sequence == null)
        {
            Debug.LogWarning($"[DialogueTrigger] '{gameObject.name}' has no DialogueSequence assigned.", this);
            return;
        }

        _fired = true;

        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[DialogueTrigger] DialogueManager.Instance is null. Is it on the GameManager?", this);
            return;
        }

        if (_interruptCurrent)
            DialogueManager.Instance.Interrupt(_sequence);
        else
            DialogueManager.Instance.Enqueue(_sequence);
    }

    /// <summary>Reset the fired flag so the trigger can fire again.</summary>
    public void ResetTrigger() => _fired = false;
}
