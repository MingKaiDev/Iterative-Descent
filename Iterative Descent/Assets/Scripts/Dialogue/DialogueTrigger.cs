using UnityEngine;

/// <summary>
/// Plays a DialogueSequence either when the player enters a trigger collider,
/// or when the player spawns into the scene (via PlayerMovement.OnPlayerSpawned).
///
/// Spawn mode exists because trigger colliders are unreliable for "first thing
/// that happens in the level" dialogue: if the player already overlaps the
/// collider at scene load instead of walking into it, OnTriggerEnter can fail
/// to fire depending on load path / physics timing. Spawn mode sidesteps that
/// entirely by not depending on physics at all.
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
public class DialogueTrigger : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Dialogue")]
    [Tooltip("The sequence to play when triggered.")]
    [SerializeField] private DialogueSequence _sequence;

    [Header("Trigger Mode")]
    [Tooltip("If true, fires when the player spawns into the scene (PlayerMovement.OnPlayerSpawned) " +
             "instead of via a trigger collider. Use this for the very first dialogue in a level - " +
             "no Collider is needed on this GameObject in this mode.")]
    [SerializeField] private bool _triggerOnPlayerSpawn = false;

    [Tooltip("Optional delay (seconds) after the spawn event before the dialogue plays. " +
             "Only used when Trigger On Player Spawn is enabled. Useful if a loading fade " +
             "or intro camera move needs to finish first.")]
    [SerializeField] private float _spawnDelay = 0f;

    [Header("Behaviour")]
    [Tooltip("If true the trigger fires only once. Recommended for story beats.")]
    [SerializeField] private bool _playOnce = true;

    [Tooltip("If true the new sequence cuts off whatever is currently playing and clears the queue. " +
             "If false the sequence is added to the back of the queue.")]
    [SerializeField] private bool _interruptCurrent = false;

    [Tooltip("Tag of the GameObject that activates this trigger. Leave blank to accept any tag. " +
             "Not used in spawn mode.")]
    [SerializeField] private string _requiredTag = "Player";

    // ─── State ───────────────────────────────────────────────────────────────────

    private bool _fired = false;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (_triggerOnPlayerSpawn)
            PlayerMovement.OnPlayerSpawned += HandlePlayerSpawned;
    }

    void OnDisable()
    {
        if (_triggerOnPlayerSpawn)
            PlayerMovement.OnPlayerSpawned -= HandlePlayerSpawned;
    }

    void OnTriggerEnter(Collider other)
    {

        if (_triggerOnPlayerSpawn) return; // spawn mode does not use the collider path

        if (_playOnce && _fired) return;

        if (!string.IsNullOrEmpty(_requiredTag) && !other.CompareTag(_requiredTag)) return;

        PlayDialogue();
    }

    void HandlePlayerSpawned()
    {
        if (_spawnDelay > 0f)
            Invoke(nameof(PlayDialogue), _spawnDelay);
        else
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
