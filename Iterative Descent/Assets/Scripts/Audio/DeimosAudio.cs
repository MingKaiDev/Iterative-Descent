using UnityEngine;

/// <summary>
/// Handles non-attack audio for Deimos: combat-start stinger, footsteps, phase 2
/// transition, death sound, and combat music. Attach to the same root GameObject as
/// DeimosStateMachine / DeimosHealth / DeimosAttackRegistry (the Deimos root, not a
/// child) so it shares the AudioSource that DeimosAttackBase.PlaySound() also uses.
///
/// Mirrors BossAudio.cs (ARES/DROID-7's equivalent, Assets/Scripts/Audio/BossAudio.cs)
/// field-for-field, with one structural difference in how the opening cue fires. ARES
/// needs an external BossEncounterTrigger (an arena-gate collider) because entering
/// ARES's arena is its own distinct moment -- HUD, sealed doors, intro dialogue, and
/// Packet Filter combat-AI gating all hang off that same trigger, and BossAudio's
/// PlayEncounterStinger()/PlayBossMusic() are called from it directly. Deimos has none
/// of that yet -- it detects the player itself via DeimosStateMachine.detectionRadius
/// and walks straight into Combat, so there is no separate "commit to the arena"
/// collider to hang a call off of. Instead this class self-subscribes to
/// DeimosStateMachine.OnCombatStart (a new static event added alongside this file,
/// fired once from EnterState() the same instant the "FightStarted" Animator trigger
/// fires) -- the same "many independent listeners, no central coordinator" shape this
/// class already needs anyway for DeimosHealth.OnPhaseTwo/OnBossDeath, just extended to
/// cover the fight-start case too. No Inspector wiring or extra trigger collider needed
/// for the stinger/music -- just add this component to the Deimos root and assign clips.
///
/// Footsteps are NOT wired automatically -- add an Animation Event on Deimos's Walk
/// clip calling PlayFootstep(), same as BossAudio.PlayFootstep() / EnemyChaserAudio.
/// PlayWalkStep().
///
/// Combat music is owned by GameAudioManager, not a separate AudioSource here -- same
/// reasoning and the same call sites (PlayBossMusic() / StopBossMusic()) as BossAudio:
/// it's still just "the BGM," a different track during the fight, hard-swapped rather
/// than crossfaded. ARES and Deimos never fight at the same time in this game, so
/// reusing GameAudioManager's single BGM source and its existing PlayBossMusic()/
/// StopBossMusic() names (rather than adding Deimos-specific ones there) is safe and
/// avoids a second near-duplicate method pair.
///
/// Per-attack windup/hitbox-open clips are a SEPARATE system and already exist --
/// DeimosAttackBase (like BossAttackBase) carries its own windupClip/hitboxOpenClip
/// fields, inherited by every attack (MMAKickAttack, DropkickAttack, and future ones).
/// This component only covers the non-attack cues listed above.
///
/// All AudioClip fields are optional and default to null. No Deimos SFX or music
/// assets exist in Assets/Audio yet -- see FYP/setup-guides/DeimosAudio_UnitySetup.md.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class DeimosAudio : MonoBehaviour
{
    [Header("Combat Start")]
    [Tooltip("Plays once the instant Deimos detects the player and enters Combat " +
             "(DeimosStateMachine.OnCombatStart) -- the same moment the 'FightStarted' " +
             "Animator trigger fires.")]
    public AudioClip combatStingerClip;

    [Header("Footsteps")]
    [Tooltip("Drag a Deimos footstep clip here. Called via Animation Event on the Walk clip.")]
    public AudioClip footstepClip;

    [Header("Phase 2")]
    [Tooltip("Plays once when Deimos crosses the Phase 2 HP threshold (DeimosHealth.OnPhaseTwo, " +
             "default 50% of maxHealth). The HP-threshold event itself already fires today -- " +
             "only a behavioural Phase 2 reaction is still pending, same as ARES's own Phase 2 " +
             "status before its combat changes were wired.")]
    public AudioClip phaseTwoClip;

    [Header("Hit / Stagger")]
    [Tooltip("Plays each time Deimos's DPS-threshold flinch triggers (DeimosHealth.OnStaggerStart " +
             "-- see DeimosHealth.cs's class doc comment), alongside the 'Hit' Animator trigger " +
             "and Hit To Body clip DeimosStateMachine fires at the same moment. Optional -- " +
             "leave null for a silent flinch.")]
    public AudioClip hitClip;

    [Header("Death")]
    [Tooltip("Plays once when Deimos dies (DeimosHealth.OnBossDeath).")]
    public AudioClip deathClip;

    [Header("Combat Music")]
    [Tooltip("Looping music clip for the fight. Starts the instant Deimos enters Combat, stops " +
             "when Deimos dies. Stops GameAudioManager's ambient loop while playing and resumes " +
             "it on death/stop.")]
    public AudioClip combatMusicClip;

    [Range(0f, 1f)] public float combatStingerVolume = 1f;
    [Range(0f, 1f)] public float footstepVolume       = 0.7f;
    [Range(0f, 1f)] public float phaseTwoVolume       = 1f;
    [Range(0f, 1f)] public float hitVolume            = 0.8f;
    [Range(0f, 1f)] public float deathVolume          = 1f;
    [Range(0f, 1f)] public float musicVolume          = 0.6f;

    // ─── Private ────────────────────────────────────────────────────────────────
    private AudioSource _audioSource;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 1f;   // full 3D so distance attenuation applies
        _audioSource.playOnAwake  = false;

        DeimosStateMachine.OnCombatStart += HandleCombatStart;
        DeimosHealth.OnPhaseTwo          += HandlePhaseTwo;
        DeimosHealth.OnStaggerStart      += HandleStagger;
        DeimosHealth.OnBossDeath         += HandleBossDeath;
    }

    void OnDestroy()
    {
        DeimosStateMachine.OnCombatStart -= HandleCombatStart;
        DeimosHealth.OnPhaseTwo          -= HandlePhaseTwo;
        DeimosHealth.OnStaggerStart      -= HandleStagger;
        DeimosHealth.OnBossDeath         -= HandleBossDeath;
    }

    // ─── Animation Event Target ───────────────────────────────────────────────────

    /// <summary>Called by an Animation Event on Deimos's Walk clip at each footfall frame.</summary>
    public void PlayFootstep()
    {
        if (footstepClip == null) return; // silent by design until a clip is assigned
        _audioSource.PlayOneShot(footstepClip, footstepVolume);
    }

    // ─── Event Handlers ─────────────────────────────────────────────────────────

    void HandleCombatStart()
    {
        if (combatStingerClip == null)
            Debug.LogWarning("[DeimosAudio] No combat stinger clip assigned.");
        else
            _audioSource.PlayOneShot(combatStingerClip, combatStingerVolume);

        PlayCombatMusic();
    }

    void HandlePhaseTwo()
    {
        if (phaseTwoClip == null)
        {
            Debug.LogWarning("[DeimosAudio] No phase two clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(phaseTwoClip, phaseTwoVolume);
    }

    void HandleStagger()
    {
        if (hitClip == null) return; // silent by design until a clip is assigned -- unlike
                                      // phaseTwoClip/deathClip, a stagger can happen many times
                                      // per fight, so a missing clip warning every time would
                                      // just spam the console.
        _audioSource.PlayOneShot(hitClip, hitVolume);
    }

    void HandleBossDeath()
    {
        StopCombatMusic();

        if (deathClip == null)
        {
            Debug.LogWarning("[DeimosAudio] No death clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(deathClip, deathVolume);
    }

    // ─── Combat Music (forwards to GameAudioManager, same as BossAudio) ─────────────

    void PlayCombatMusic()
    {
        if (combatMusicClip == null)
        {
            Debug.LogWarning("[DeimosAudio] No combat music clip assigned.");
            return;
        }
        if (GameAudioManager.Instance == null)
        {
            Debug.LogWarning("[DeimosAudio] GameAudioManager.Instance is null -- combat music will not play.");
            return;
        }
        GameAudioManager.Instance.PlayBossMusic(combatMusicClip, musicVolume);
    }

    void StopCombatMusic()
    {
        if (GameAudioManager.Instance != null)
            GameAudioManager.Instance.StopBossMusic();
    }
}
