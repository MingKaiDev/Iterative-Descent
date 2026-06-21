using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton that drives all dialogue playback.
/// Attach to the persistent GameManager GameObject (DontDestroyOnLoad).
///
/// Usage from code:
///   DialogueManager.Instance.Enqueue(mySequence);         // queue after current line finishes
///   DialogueManager.Instance.Interrupt(mySequence);       // cut off current dialogue immediately
///
/// Input during narration  : [Space]  - skip typewriter / advance to next line
/// Input during choices     : [1] [2] [3] - select a response option
///
/// Why Space and not E?
///   E is already used by PlayerInteractor. Using Space avoids the player accidentally
///   interacting with a world object while trying to skip dialogue.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────────────────────────

    public static DialogueManager Instance { get; private set; }

    // ─── Events ──────────────────────────────────────────────────────────────────

    /// <summary>Fired when the very first entry of a new sequence begins playing.</summary>
    public static event Action OnDialogueStarted;

    /// <summary>Fired when a sequence fully ends (all entries done, or dialogue cleared).</summary>
    public static event Action OnDialogueEnded;

    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The SubtitleUI component on the Canvas. Assign in Inspector.")]
    [SerializeField] private SubtitleUI _subtitleUI;

    [Header("Typewriter")]
    [Tooltip("Characters revealed per second during typewriter animation.")]
    [SerializeField] private float _charsPerSecond = 35f;

    [Header("Voice Acting")]
    [Tooltip("AudioSource used for voice clips. Assign a dedicated AudioSource on the GameManager. " +
             "If left null, voice clips will not play.")]
    [SerializeField] private AudioSource _voiceSource;

    // ─── State ───────────────────────────────────────────────────────────────────

    private Queue<DialogueSequence> _queue = new();
    private DialogueSequence        _current;
    private int                     _entryIndex;
    private Coroutine               _playRoutine;
    private bool                    _typewriterDone;
    private bool                    _waitingForChoice;
    private bool                    _didPauseInteractor;

    public bool IsPlaying        => _current != null;
    public bool WaitingForChoice => _waitingForChoice;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (!IsPlaying) return;

        if (_waitingForChoice)
        {
            HandleChoiceInput();
            return;
        }

        // Space: skip typewriter reveal first press; advance line on second press.
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            HandleSkip();
    }

    // ─── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Add a sequence to the back of the queue.
    /// If nothing is playing it starts immediately.
    /// </summary>
    public void Enqueue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.entries == null || sequence.entries.Length == 0) return;

        if (!IsPlaying)
            BeginSequence(sequence);
        else
            _queue.Enqueue(sequence);
    }

    /// <summary>
    /// Stop whatever is playing and start this sequence immediately.
    /// Clears the entire queue.
    /// </summary>
    public void Interrupt(DialogueSequence sequence)
    {
        if (sequence == null) return;
        StopCurrentRoutine();
        _queue.Clear();
        BeginSequence(sequence);
    }

    /// <summary>
    /// Called by SubtitleUI choice buttons or by DialogueManager.Update keyboard input.
    /// index is 0-based (matches choices array).
    /// </summary>
    public void SelectChoice(int index)
    {
        if (!_waitingForChoice) return;

        var entry = CurrentEntry();
        if (entry == null || !entry.HasChoices) return;
        if (index < 0 || index >= entry.choices.Length) return;

        ResumeInteractorIfNeeded();
        _waitingForChoice = false;

        var chosen = entry.choices[index];
        if (chosen.nextSequence != null)
        {
            // Branch: interrupt current sequence with the chosen one.
            StopCurrentRoutine();
            _queue.Clear();
            BeginSequence(chosen.nextSequence);
        }
        else
        {
            // No branch: end the dialogue.
            EndDialogue();
        }
    }

    // ─── Internals ───────────────────────────────────────────────────────────────

    void BeginSequence(DialogueSequence sequence)
    {
        _current    = sequence;
        _entryIndex = 0;
        OnDialogueStarted?.Invoke();
        _playRoutine = StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        while (_entryIndex < _current.entries.Length)
        {
            var entry = _current.entries[_entryIndex];
            yield return StartCoroutine(PlayEntry(entry));
            _entryIndex++;
        }

        // Sequence exhausted — check queue for next.
        _current = null;

        if (_queue.Count > 0)
            BeginSequence(_queue.Dequeue());
        else
            EndDialogue();
    }

    void PlayVoiceClip(AudioClip clip)
    {
        if (_voiceSource == null || clip == null) return;
        _voiceSource.Stop();
        _voiceSource.clip = clip;
        _voiceSource.Play();
    }

    void StopVoiceClip()
    {
        if (_voiceSource != null && _voiceSource.isPlaying)
            _voiceSource.Stop();
    }

    IEnumerator PlayEntry(DialogueEntry entry)
    {
        _typewriterDone = false;

        // Start voice clip independently; typewriter runs at its own pace.
        PlayVoiceClip(entry.voiceClip);

        // Kick off typewriter reveal.
        yield return StartCoroutine(TypewriterRoutine(entry));

        _typewriterDone = true;

        if (entry.HasChoices)
        {
            // Pause world interaction while player reads choices.
            PauseInteractorIfNeeded();
            _waitingForChoice = true;
            _subtitleUI.ShowChoices(entry.choices);

            // Wait until SelectChoice() clears the flag.
            yield return new WaitUntil(() => !_waitingForChoice);
        }
        else
        {
            // Auto-dismiss after reading time.
            float timer = 0f;
            float duration = entry.GetDuration();
            while (timer < duration)
            {
                // Space again after typewriter is done skips the wait.
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    StopVoiceClip();
                    break;
                }
                timer += Time.unscaledDeltaTime; // unscaledDeltaTime: safe when timeScale=0
                yield return null;
            }
            _subtitleUI.HideLine();
        }
    }

    IEnumerator TypewriterRoutine(DialogueEntry entry)
    {
        _subtitleUI.BeginLine(entry.speakerName, entry.text);

        int totalChars = entry.text != null ? entry.text.Length : 0;
        float delay = _charsPerSecond > 0f ? 1f / _charsPerSecond : 0f;
        int revealed = 0;

        while (revealed < totalChars)
        {
            // Space during typewriter: snap to full text immediately and stop voice.
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StopVoiceClip();
                _subtitleUI.RevealFull();
                yield break;
            }

            revealed++;
            _subtitleUI.RevealChars(revealed);
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    void HandleSkip()
    {
        // If typewriter is still running the coroutine handles the skip itself.
        // This path only fires when typewriter is done and we are in the auto-dismiss wait.
        // The auto-dismiss coroutine already checks Space directly, so nothing extra needed here.
    }

    void HandleChoiceInput()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SelectChoice(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) SelectChoice(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) SelectChoice(2);
    }

    void PauseInteractorIfNeeded()
    {
        if (!PlayerInteractor.IsPaused)
        {
            PlayerInteractor.Pause();
            _didPauseInteractor = true;
        }
    }

    void ResumeInteractorIfNeeded()
    {
        if (_didPauseInteractor)
        {
            PlayerInteractor.Resume();
            _didPauseInteractor = false;
        }
    }

    void EndDialogue()
    {
        _subtitleUI?.HideLine();
        _subtitleUI?.HideChoices();
        ResumeInteractorIfNeeded();
        _waitingForChoice = false;
        _current = null;
        OnDialogueEnded?.Invoke();
    }

    void StopCurrentRoutine()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        StopVoiceClip();
        ResumeInteractorIfNeeded();
        _waitingForChoice = false;
    }

    DialogueEntry CurrentEntry()
    {
        if (_current == null) return null;
        if (_entryIndex < 0 || _entryIndex >= _current.entries.Length) return null;
        return _current.entries[_entryIndex];
    }

    void OnDestroy()
    {
        StopCurrentRoutine();
    }
}
