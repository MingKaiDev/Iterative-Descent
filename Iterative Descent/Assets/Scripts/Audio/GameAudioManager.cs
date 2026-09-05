using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Scene-level audio manager. Handles looping BGM -- ambient background noise by
/// default, or boss music while a fight is active. Attach to a dedicated
/// AudioManager GameObject in the scene. Does NOT use DontDestroyOnLoad so the
/// ambience can differ per scene.
///
/// There is one BGM AudioSource, not two. Boss music (via PlayBossMusic()) and
/// any other level music cue (via the general PlayMusic(), e.g. from
/// BGMTrigger.cs) hard-swap that same source's clip instead of ducking a
/// separate ambient source under a second one -- it's still "the background
/// music," just a different track. BossAudio (Assets/Scripts/Audio/BossAudio.cs)
/// owns the boss music clip reference and calls PlayBossMusic()/StopBossMusic()
/// here; it does not keep its own AudioSource for it. PlayBossMusic() is a thin
/// wrapper over PlayMusic(clip, volume, loop: true) kept for backwards
/// compatibility with existing BossAudio call sites.
///
/// AudioSource.volume here (ambientVolume) is the per-clip design volume, same
/// as every other audio script in the project (e.g. footstepVolume). The
/// player's Music slider in Settings is a separate multiplier applied via the
/// AudioMixer this source's Output is routed to, not by touching this field --
/// see SettingsManager.ApplyVolumeToMixer() and
/// FYP/setup-guides/VolumeSettings_UnitySetup.md.
///
/// AUTO-REVERT: a music override started with loop=false (a one-shot BGMTrigger
/// stinger, say) automatically reverts back to the scene's ambient clip the
/// instant it finishes playing on its own -- see Update() below. Looping
/// overrides (boss music, Phobos's encounter music, or any BGMTrigger with
/// loop=true) never auto-revert; something has to explicitly call
/// StopBossMusic() for those (boss death, Brute death, etc).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Ambient")]
    [Tooltip("Drag Background Noise 1 here.")]
    public AudioClip backgroundNoiseClip;

    [Range(0f, 1f)]
    public float ambientVolume = 0.4f;

    [Header("Volume Settings")]
    [Tooltip("Project AudioMixer asset (Music/SFX groups). Assign so the saved " +
             "Settings volume is applied on scene load even if the player never " +
             "opens the Settings panel. Safe to leave empty during early setup.")]
    [SerializeField] private AudioMixer audioMixer;

    // ─── Private ────────────────────────────────────────────────────────────────
    private AudioSource _ambientSource;
    private bool _musicOverrideActive;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _ambientSource = GetComponent<AudioSource>();
        ConfigureAmbientSource();

        SettingsManager.ApplyVolumeToMixer(audioMixer);
    }

    void Start()
    {
        PlayAmbient();
    }

    void Update()
    {
        // Auto-revert to ambient once a NON-LOOPING music override finishes playing on
        // its own. The ambient loop and every looping override (boss music, Phobos's
        // encounter music, a looping BGMTrigger) always have Loop=true, so this only
        // ever fires for a one-shot cue -- nothing else needs to remember to call
        // StopBossMusic() for those.
        if (_musicOverrideActive && !_ambientSource.loop && !_ambientSource.isPlaying)
            StopBossMusic();
    }

    // ─── Ambient ────────────────────────────────────────────────────────────────

    void ConfigureAmbientSource()
    {
        _ambientSource.clip        = backgroundNoiseClip;
        _ambientSource.loop        = true;
        _ambientSource.volume      = ambientVolume;
        _ambientSource.spatialBlend = 0f;   // 2D audio - same volume everywhere
        _ambientSource.playOnAwake  = false;
    }

    void PlayAmbient()
    {
        if (backgroundNoiseClip == null)
        {
            Debug.LogWarning("[GameAudioManager] No background noise clip assigned.");
            return;
        }
        _ambientSource.Play();
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Stop the ambient loop (e.g. during cutscenes or jump scares).</summary>
    public void StopAmbient()  => _ambientSource.Stop();

    /// <summary>Resume the ambient loop after stopping it.</summary>
    public void StartAmbient() => _ambientSource.Play();

    public void SetAmbientVolume(float volume)
    {
        ambientVolume = Mathf.Clamp01(volume);
        if (!_musicOverrideActive)
            _ambientSource.volume = ambientVolume;
    }

    // ─── Boss Music ─────────────────────────────────────────────────────────────

    /// <summary>Hard-swaps the BGM source to any clip -- boss music, a room-specific
    /// track, a one-shot stinger, etc. -- with control over whether it loops.
    /// Does not crossfade -- whatever was playing stops the instant this plays.
    /// Used internally by PlayBossMusic() (always loop=true) and directly by
    /// BGMTrigger.cs for arbitrary music cues placed around the level.</summary>
    public void PlayMusic(AudioClip clip, float volume, bool loop)
    {
        if (clip == null)
        {
            Debug.LogWarning("[GameAudioManager] PlayMusic called with a null clip.");
            return;
        }

        _musicOverrideActive = true;
        _ambientSource.Stop();
        _ambientSource.clip   = clip;
        _ambientSource.loop   = loop;
        _ambientSource.volume = volume;
        _ambientSource.Play();
    }

    /// <summary>Hard-swaps the BGM source to a boss music clip. Called by BossAudio
    /// on arena entry. Thin wrapper over PlayMusic() with loop always true --
    /// kept as its own method so existing BossAudio call sites don't change.</summary>
    public void PlayBossMusic(AudioClip clip, float volume) => PlayMusic(clip, volume, loop: true);

    /// <summary>Swaps the BGM source back to the scene's ambient clip. Called by
    /// BossAudio on boss death. No-op if no music override (boss music or a
    /// BGMTrigger cue) was ever started, so it can't restart (and audibly glitch)
    /// an ambient track that was never interrupted.</summary>
    public void StopBossMusic()
    {
        if (!_musicOverrideActive) return;
        _musicOverrideActive = false;

        _ambientSource.Stop();
        _ambientSource.clip   = backgroundNoiseClip;
        _ambientSource.loop   = true;
        _ambientSource.volume = ambientVolume;

        if (backgroundNoiseClip != null)
            _ambientSource.Play();
    }
}
