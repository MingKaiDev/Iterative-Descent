using UnityEngine;

/// <summary>
/// Handles audio for EnemyBrute (Phobos): the wakeup scream and walk footsteps.
/// Attach to the same root GameObject as EnemyBrute (the Phobos prefab root),
/// same convention as EnemyChaserAudio.
///
/// Footsteps are driven by an Animation Event on the "Mutant Walk" clip:
///   Mutant Walk animation -> calls PlayFootstep() (wired via the FBX import
///   settings on Mutant Walking (1).fbx -- two events per gait cycle, one per
///   footfall. Times are an ESTIMATE based on typical Mixamo walk-cycle timing
///   (~36% / ~61% through the clip) -- not yet visually confirmed against the
///   actual footfall frames. If footsteps sound out of sync with the feet in
///   Play mode, open Mutant Walking (1).fbx's Animation import tab and drag the
///   two PlayFootstep events to the real contact frames, the same way
///   BruteAttack's swipe hit-frame estimates are meant to be corrected.
///
/// Scream is called directly by EnemyBrute.WakeUpRoutine() via GetComponent, at
/// the same moment the "Scream" Animator trigger fires -- not an Animation Event,
/// since WakeUpRoutine already owns that exact timing (wakeupLockDuration is
/// tuned to the Scream clip's length).
///
/// grabConnectClip and neckSnapClip are both called directly by BruteAttack via
/// GetComponent, same convention as Scream -- BruteAttack already owns the exact
/// timing for both moments (GrabPlayer() the instant the hitbox connects,
/// ResolveGrabOutcome() at the end of the animation), so neither needs an
/// Animation Event:
///   PlayGrabConnect() -- the moment the grab hitbox touches the player (a grip/
///                         impact sound -- the "you're caught" beat).
///   PlayNeckSnap()    -- the moment the instant kill actually resolves, at the
///                         end of the Thrusting Attack clip (see BruteAttack's
///                         "Grab redesign" doc comment) -- the kill sound itself.
///
/// Uses a 3D spatialBlend so the sound falls off with distance, same as
/// EnemyChaserAudio -- the player hears a closer Brute more clearly.
///
/// ENCOUNTER MUSIC: separately from the positional creature SFX above, this
/// component also owns the Brute's own BGM cue -- PlayEncounterMusic() hard-
/// swaps the scene's single BGM source (via GameAudioManager.PlayMusic(),
/// looping) the moment EnemyBrute.OnActivate() fires (encounter start), and
/// StopEncounterMusic() swaps it back to the scene's ambient noise the moment
/// EnemyBrute.OnDie() fires (encounter end). Same pattern as BossAudio's
/// PlayBossMusic()/StopBossMusic() for ARES, just triggered by this Brute's
/// own activate/death instead of a dedicated encounter-trigger script.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BruteAudio : MonoBehaviour
{
    [Header("Brute SFX")]
    [Tooltip("Drag Assets/Audio/Enemy/Phobos/Footstep.wav here.")]
    public AudioClip footstepClip;

    [Tooltip("Drag Assets/Audio/Enemy/Phobos/Scream.wav here. Plays once per " +
             "Activate() at the same moment the Scream animator trigger fires.")]
    public AudioClip screamClip;

    [Tooltip("Drag a grip/impact clip here -- played the instant the grab hitbox " +
             "connects (BruteAttack.GrabPlayer). The \"you're caught\" beat. Optional -- " +
             "safe to leave unassigned.")]
    public AudioClip grabConnectClip;

    [Tooltip("Drag a neck-snap clip here -- played at the end of the Grab animation, the " +
             "instant the instant kill actually resolves (BruteAttack.ResolveGrabOutcome). " +
             "Does NOT play if the grab is released early without a kill (e.g. the Brute " +
             "is destroyed mid-grab -- see BruteAttack.ReleaseGrabIfActive). Optional -- " +
             "safe to leave unassigned.")]
    public AudioClip neckSnapClip;

    [Tooltip("Drag Assets/Audio/Enemy/Phobos/<your death clip> here -- played once from " +
             "EnemyBrute.OnDie(), same moment the \"IsDead\" Animator bool is set. Optional, " +
             "same convention as EnemyChaserAudio.deathClip -- safe to leave unassigned.")]
    public AudioClip deathClip;

    [Range(0f, 1f)]
    public float footstepVolume = 0.7f;
    [Range(0f, 1f)]
    public float screamVolume = 1f;
    [Range(0f, 1f)]
    public float grabConnectVolume = 0.9f;
    [Range(0f, 1f)]
    public float neckSnapVolume = 1f;
    [Range(0f, 1f)]
    public float deathVolume = 1f;

    [Header("Encounter Music")]
    [Tooltip("Music that hard-swaps in as the scene's BGM the moment this Brute's " +
             "encounter starts (EnemyBrute.OnActivate()) -- loops for the whole fight. " +
             "Optional -- leave empty to skip the music swap entirely and only use the " +
             "Scream/footstep/death SFX above.")]
    public AudioClip encounterMusicClip;

    [Range(0f, 1f)]
    public float encounterMusicVolume = 0.35f;

    // ─── Private ─────────────────────────────────────────────────────────────────

    private AudioSource _audioSource;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 1f;   // full 3D so distance attenuation applies
        _audioSource.playOnAwake  = false;
    }

    // ─── Animation Event Target ───────────────────────────────────────────────────

    /// <summary>
    /// Called by an Animation Event on the "Mutant Walk" clip at each footfall frame.
    /// </summary>
    public void PlayFootstep()
    {
        if (footstepClip == null)
        {
            Debug.LogWarning("[BruteAudio] No footstep clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(footstepClip, footstepVolume);
    }

    // ─── Called by EnemyBrute ─────────────────────────────────────────────────────

    /// <summary>
    /// Called from EnemyBrute.WakeUpRoutine(), the moment the Scream Animator
    /// trigger fires -- fires every Activate(), including re-activations, same
    /// as the trigger itself.
    /// </summary>
    public void PlayScream()
    {
        if (screamClip == null)
        {
            Debug.LogWarning("[BruteAudio] No scream clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(screamClip, screamVolume);
    }

    /// <summary>
    /// Called from BruteAttack.GrabPlayer(), the instant the grab hitbox connects with
    /// the player -- fires alongside the camera shake pulse and the forced camera lock.
    /// </summary>
    public void PlayGrabConnect()
    {
        if (grabConnectClip == null)
        {
            Debug.LogWarning("[BruteAudio] No grab-connect clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(grabConnectClip, grabConnectVolume);
    }

    /// <summary>
    /// Called from BruteAttack.ResolveGrabOutcome(), the moment the instant kill actually
    /// resolves at the end of the Grab animation. Not called on a released-without-kill
    /// grab (BruteAttack.ReleaseGrabIfActive) -- that path is an escape, not a kill, so it
    /// stays silent here.
    /// </summary>
    public void PlayNeckSnap()
    {
        if (neckSnapClip == null)
        {
            Debug.LogWarning("[BruteAudio] No neck-snap clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(neckSnapClip, neckSnapVolume);
    }

    /// <summary>
    /// Called from EnemyBrute.OnDie(), the same moment the "IsDead" Animator bool is
    /// set (drives the Death animation state -- see Phobos.controller's Any State ->
    /// Death transition). PlayOneShot at the Brute's position -- the AudioSource
    /// component survives the Destroy(gameObject, 3f) delay in OnDie() long enough
    /// for the clip to finish, same reasoning as EnemyChaserAudio.PlayDeath().
    /// </summary>
    public void PlayDeath()
    {
        if (deathClip == null)
        {
            Debug.LogWarning("[BruteAudio] No death clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(deathClip, deathVolume);
    }

    // ─── Encounter Music (scene BGM, not this AudioSource) ───────────────────────

    /// <summary>
    /// Called from EnemyBrute.OnActivate() -- hard-swaps the scene's single BGM
    /// source to encounterMusicClip via GameAudioManager, looping for the duration
    /// of the fight. No-op if encounterMusicClip is unassigned, so a Brute with no
    /// encounter track keeps whatever BGM/ambient was already playing.
    /// </summary>
    public void PlayEncounterMusic()
    {
        if (encounterMusicClip == null) return;

        if (GameAudioManager.Instance == null)
        {
            Debug.LogWarning("[BruteAudio] GameAudioManager.Instance is null -- encounter music will not play.");
            return;
        }

        GameAudioManager.Instance.PlayMusic(encounterMusicClip, encounterMusicVolume, loop: true);
    }

    /// <summary>
    /// Called from EnemyBrute.OnDie() -- swaps the scene's BGM back to its ambient
    /// clip. No-op if encounterMusicClip was never assigned (nothing to revert), or
    /// if GameAudioManager never actually started an override (e.g. boss music is
    /// what's really playing right now, not this Brute's track) -- StopBossMusic()
    /// already guards on _musicOverrideActive internally.
    /// </summary>
    public void StopEncounterMusic()
    {
        if (encounterMusicClip == null) return;
        GameAudioManager.Instance?.StopBossMusic();
    }
}
