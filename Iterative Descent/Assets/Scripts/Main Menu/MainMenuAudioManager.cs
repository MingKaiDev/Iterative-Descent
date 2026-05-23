using UnityEngine;

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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
