using UnityEngine;

/// <summary>
/// Scene-level audio manager. Handles looping BGM -- ambient background noise by
/// default, or boss music while a fight is active. Attach to a dedicated
/// AudioManager GameObject in the scene. Does NOT use DontDestroyOnLoad so the
/// ambience can differ per scene.
///
/// There is one BGM AudioSource, not two. Boss music (via PlayBossMusic()) hard-
/// swaps that source's clip instead of ducking a separate ambient source under a
/// second one -- it's still "the background music," just a different track for
/// the fight. BossAudio (Assets/Scripts/Audio/BossAudio.cs) owns the boss music
/// clip reference and calls PlayBossMusic()/StopBossMusic() here; it does not
/// keep its own AudioSource for it.
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

    // ─── Private ────────────────────────────────────────────────────────────────
    private AudioSource _ambientSource;
    private bool _bossMusicActive;

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
    }

    void Start()
    {
        PlayAmbient();
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
        if (!_bossMusicActive)
            _ambientSource.volume = ambientVolume;
    }

    // ─── Boss Music ─────────────────────────────────────────────────────────────

    /// <summary>Hard-swaps the BGM source to a boss music clip. Called by BossAudio
    /// on arena entry. Does not crossfade -- the ambient clip stops the instant
    /// this plays.</summary>
    public void PlayBossMusic(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            Debug.LogWarning("[GameAudioManager] PlayBossMusic called with a null clip.");
            return;
        }

        _bossMusicActive = true;
        _ambientSource.Stop();
        _ambientSource.clip   = clip;
        _ambientSource.volume = volume;
        _ambientSource.Play();
    }

    /// <summary>Swaps the BGM source back to the scene's ambient clip. Called by
    /// BossAudio on boss death. No-op if boss music was never started, so it can't
    /// restart (and audibly glitch) an ambient track that was never interrupted.</summary>
    public void StopBossMusic()
    {
        if (!_bossMusicActive) return;
        _bossMusicActive = false;

        _ambientSource.Stop();
        _ambientSource.clip   = backgroundNoiseClip;
        _ambientSource.volume = ambientVolume;

        if (backgroundNoiseClip != null)
            _ambientSource.Play();
    }
}
