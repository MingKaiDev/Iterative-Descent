using UnityEngine;

/// <summary>
/// Handles all player audio: weapon SFX and footsteps.
/// Attach to the same GameObject as PlayerMovement and PlayerCombat.
///
/// Footstep sounds are triggered by Animation Events on the walk/run clips:
///   Walk animation -> calls PlayWalkStep()
///   Run animation  -> calls PlayRunStep()
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerAudioController : MonoBehaviour
{
    [Header("Pistol SFX")]
    [Tooltip("Drag Pistol Shot here.")]
    public AudioClip pistolShotClip;

    [Tooltip("Drag Pistol Reload here.")]
    public AudioClip pistolReloadClip;

    [Range(0f, 1f)]
    public float shotVolume   = 0.9f;
    [Range(0f, 1f)]
    public float reloadVolume = 0.8f;

    [Header("Footstep SFX")]
    [Tooltip("Drag Player Walk footstep clip here.")]
    public AudioClip walkStepClip;

    [Tooltip("Drag Player Run footstep clip here.")]
    public AudioClip runStepClip;

    [Range(0f, 1f)]
    public float footstepVolume = 0.6f;

    // ─── Private ────────────────────────────────────────────────────────────────
    private AudioSource _audioSource;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake  = false;
    }

    void OnEnable()
    {
        PlayerCombat.OnFired       += HandleFired;
        PlayerCombat.OnReloadStart += HandleReloadStart;
    }

    void OnDisable()
    {
        PlayerCombat.OnFired       -= HandleFired;
        PlayerCombat.OnReloadStart -= HandleReloadStart;
    }

    // ─── Handlers ───────────────────────────────────────────────────────────────

    void HandleFired()
    {
        if (pistolShotClip == null)
        {
            Debug.LogWarning("[PlayerAudioController] No pistol shot clip assigned.");
            return;
        }
        // PlayOneShot allows overlapping - essential for rapid fire.
        _audioSource.PlayOneShot(pistolShotClip, shotVolume);
    }

    void HandleReloadStart()
    {
        if (pistolReloadClip == null)
        {
            Debug.LogWarning("[PlayerAudioController] No pistol reload clip assigned.");
            return;
        }
        // Stop any previous reload sound (shouldn't overlap) then play.
        _audioSource.Stop();
        _audioSource.PlayOneShot(pistolReloadClip, reloadVolume);
    }

    // ─── Footstep Handlers (called by Animation Events) ─────────────────────────

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
}
