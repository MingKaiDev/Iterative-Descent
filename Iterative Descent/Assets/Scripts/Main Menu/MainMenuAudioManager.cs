using UnityEngine;
using UnityEngine.Audio;

// bgmVolume/sfxVolume below are per-clip design volumes (same role as every
// other *Volume field in the project's audio scripts), not the player's
// Settings sliders. The Settings Music/SFX sliders are a separate multiplier
// applied via the AudioMixer bgmSource/sfxSource are routed to -- see
// SettingsManager.ApplyVolumeToMixer() and
// FYP/setup-guides/VolumeSettings_UnitySetup.md.
public class MainMenuAudioManager : MonoBehaviour
{
    public static MainMenuAudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] AudioSource bgmSource;
    [SerializeField] AudioSource sfxSource;

    [Header("Clips")]
    public AudioClip bgmClip;
    public AudioClip hoverClip;
    public AudioClip clickClip;

    [Header("Settings")]
    [Range(0f, 1f)] public float bgmVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Volume Settings (player-facing)")]
    [Tooltip("Project AudioMixer asset (Music/SFX groups). Assign so the saved " +
             "Settings volume is applied on scene load even if the player never " +
             "opens the Settings panel. Safe to leave empty during early setup.")]
    [SerializeField] private AudioMixer audioMixer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SettingsManager.ApplyVolumeToMixer(audioMixer);
    }

    void Start()
    {
        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }
    }

    public void PlayHover()
    {
        if (sfxSource != null && hoverClip != null)
            sfxSource.PlayOneShot(hoverClip, sfxVolume);
    }

    public void PlayClick()
    {
        if (sfxSource != null && clickClip != null)
            sfxSource.PlayOneShot(clickClip, sfxVolume);
    }
}
