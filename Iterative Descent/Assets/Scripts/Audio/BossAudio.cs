using UnityEngine;

/// <summary>
/// Handles non-attack audio for DROID-7: encounter stinger, footsteps, phase 2
/// transition, and death sound. Attach to the same root GameObject as
/// BossStateMachine / BossHealth / BossAttackRegistry (the boss root, not a
/// child) so it shares the AudioSource that BossAttackBase.PlaySound() also uses.
///
/// Mirrors the EnemyChaserAudio pattern (Assets/Scripts/Audio/EnemyChaserAudio.cs):
/// one AudioSource, PlayOneShot per clip, self-subscribed to static health events.
/// BossHUD and BossStateMachine already self-subscribe to BossHealth.OnPhaseTwo /
/// OnBossDeath the same way, so this keeps the same "many independent listeners,
/// no central coordinator" shape.
///
/// Footsteps are NOT wired automatically -- add an Animation Event on the boss
/// walk/run clip calling PlayFootstep(), same as EnemyChaserAudio.PlayWalkStep().
///
/// Encounter stinger is NOT self-triggered -- BossEncounterTrigger calls
/// PlayEncounterStinger() directly via a serialized reference, the same way it
/// already calls bossHUD.ShowHUD().
///
/// Boss music is owned by GameAudioManager, not a separate AudioSource here --
/// it's still just "the BGM," a different track during the fight. BossAudio only
/// holds the clip reference and forwards to GameAudioManager.PlayBossMusic() /
/// StopBossMusic(), which hard-swaps its single background-music AudioSource
/// (ambient stops when boss music starts, resumes when the boss dies) rather
/// than crossfading. BossEncounterTrigger calls PlayBossMusic() at the same time
/// as PlayEncounterStinger(); HandleBossDeath() stops the music automatically.
///
/// All AudioClip fields are optional and default to null. No boss SFX or music
/// assets exist in Assets/Audio yet -- see FYP/setup-guides/BossPolish_UnitySetup.md.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BossAudio : MonoBehaviour
{
    [Header("Encounter")]
    [Tooltip("Plays once when the player enters the boss arena. Called by BossEncounterTrigger.")]
    public AudioClip encounterStingerClip;

    [Header("Footsteps")]
    [Tooltip("Drag a boss footstep clip here. Called via Animation Event on the walk/run clip.")]
    public AudioClip footstepClip;

    [Header("Phase 2")]
    [Tooltip("Plays once when the boss crosses the Phase 2 HP threshold (BossHealth.OnPhaseTwo).")]
    public AudioClip phaseTwoClip;

    [Header("Death")]
    [Tooltip("Plays once when the boss dies (BossHealth.OnBossDeath).")]
    public AudioClip deathClip;

    [Header("Boss Music")]
    [Tooltip("Looping music clip for the fight. Starts when BossEncounterTrigger fires, " +
             "stops when the boss dies. Stops GameAudioManager's ambient loop while playing " +
             "and resumes it on death/stop.")]
    public AudioClip bossMusicClip;

    [Range(0f, 1f)] public float encounterVolume = 1f;
    [Range(0f, 1f)] public float footstepVolume  = 0.7f;
    [Range(0f, 1f)] public float phaseTwoVolume  = 1f;
    [Range(0f, 1f)] public float deathVolume     = 1f;
    [Range(0f, 1f)] public float musicVolume     = 0.6f;

    // ─── Private ────────────────────────────────────────────────────────────────
    private AudioSource _audioSource;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 1f;   // full 3D so distance attenuation applies
        _audioSource.playOnAwake  = false;

        BossHealth.OnPhaseTwo  += HandlePhaseTwo;
        BossHealth.OnBossDeath += HandleBossDeath;
    }

    void OnDestroy()
    {
        BossHealth.OnPhaseTwo  -= HandlePhaseTwo;
        BossHealth.OnBossDeath -= HandleBossDeath;
    }

    // ─── Called by BossEncounterTrigger ──────────────────────────────────────────

    public void PlayEncounterStinger()
    {
        if (encounterStingerClip == null)
        {
            Debug.LogWarning("[BossAudio] No encounter stinger clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(encounterStingerClip, encounterVolume);
    }

    /// <summary>Forwards to GameAudioManager, which hard-swaps its single BGM
    /// AudioSource from ambient to the boss track.</summary>
    public void PlayBossMusic()
    {
        if (bossMusicClip == null)
        {
            Debug.LogWarning("[BossAudio] No boss music clip assigned.");
            return;
        }
        if (GameAudioManager.Instance == null)
        {
            Debug.LogWarning("[BossAudio] GameAudioManager.Instance is null -- boss music will not play.");
            return;
        }
        GameAudioManager.Instance.PlayBossMusic(bossMusicClip, musicVolume);
    }

    /// <summary>Forwards to GameAudioManager, which swaps its BGM AudioSource back
    /// to the scene's ambient clip. Safe to call even if boss music was never
    /// started -- GameAudioManager no-ops in that case.</summary>
    public void StopBossMusic()
    {
        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.StopBossMusic();
    }

    // ─── Animation Event Target ───────────────────────────────────────────────────

    /// <summary>Called by an Animation Event on the boss walk/run clip at each footfall frame.</summary>
    public void PlayFootstep()
    {
        if (footstepClip == null) return; // silent by design until a clip is assigned
        _audioSource.PlayOneShot(footstepClip, footstepVolume);
    }

    // ─── Event Handlers ─────────────────────────────────────────────────────────

    void HandlePhaseTwo()
    {
        if (phaseTwoClip == null)
        {
            Debug.LogWarning("[BossAudio] No phase two clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(phaseTwoClip, phaseTwoVolume);
    }

    void HandleBossDeath()
    {
        StopBossMusic();

        if (deathClip == null)
        {
            Debug.LogWarning("[BossAudio] No death clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(deathClip, deathVolume);
    }
}
