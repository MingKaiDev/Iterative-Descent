using UnityEngine;

/// <summary>
/// Handles player footstep audio.
///
/// Normal walk + run footsteps are triggered by Animation Events on those clips
/// (PlayWalkStep / PlayRunStep).
///
/// Pistol walk (walking while aiming) uses a code-based timer instead because
/// that animation clip has no footstep events.
///
/// Weapon audio (pistol + shotgun) is handled by WeaponSounds.cs on the same GameObject.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerAudioController : MonoBehaviour
{
    [Header("Footstep SFX")]
    public AudioClip walkStepClip;
    public AudioClip runStepClip;
    [Range(0f, 1f)] public float footstepVolume = 0.6f;

    [Header("Pistol Walk Footsteps")]
    [Tooltip("Seconds between footstep sounds during pistol walk (aiming + walking). " +
             "Match this to your pistol walk animation's step cadence.")]
    public float pistolWalkStepInterval = 0.5f;

    // --- Private ------------------------------------------------------------

    private AudioSource     _audioSource;
    private PlayerMovement  _movement;
    private PlayerCombat    _pistol;

    private float _pistolWalkTimer;

    // --- Unity Lifecycle ----------------------------------------------------

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake  = false;

        _movement = GetComponent<PlayerMovement>();
        _pistol   = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        HandlePistolWalkFootsteps();
    }

    // --- Footsteps ----------------------------------------------------------

    /// <summary>Called by an Animation Event on the walk animation clip at each footfall.</summary>
    public void PlayWalkStep()
    {
        if (walkStepClip == null) return;
        _audioSource.PlayOneShot(walkStepClip, footstepVolume);
    }

    /// <summary>Called by an Animation Event on the run animation clip at each footfall.</summary>
    public void PlayRunStep()
    {
        if (runStepClip == null) return;
        _audioSource.PlayOneShot(runStepClip, footstepVolume);
    }

    // --- Pistol Walk --------------------------------------------------------

    /// <summary>
    /// Plays footsteps on a timer while the player walks with the pistol aimed.
    /// This covers the pistol-walk animation state which has no Animation Events.
    /// </summary>
    void HandlePistolWalkFootsteps()
    {
        // Only active when: pistol enabled, aiming, walking (not running).
        bool isPistolWalking = _pistol   != null && _pistol.enabled && _pistol.IsAiming
                            && _movement != null && _movement.IsWalking && !_movement.IsRunning;

        if (!isPistolWalking)
        {
            _pistolWalkTimer = 0f;
            return;
        }

        _pistolWalkTimer += Time.deltaTime;
        if (_pistolWalkTimer >= pistolWalkStepInterval)
        {
            _pistolWalkTimer -= pistolWalkStepInterval;
            PlayWalkStep();
        }
    }
}
