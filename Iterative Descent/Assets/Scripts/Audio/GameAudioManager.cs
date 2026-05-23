using UnityEngine;

/// <summary>
/// Scene-level audio manager. Handles looping ambient background music/noise.
/// Attach to a dedicated AudioManager GameObject in the scene.
/// Does NOT use DontDestroyOnLoad so the ambience can differ per scene.
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
        _ambientSource.volume = ambientVolume;
    }
}
