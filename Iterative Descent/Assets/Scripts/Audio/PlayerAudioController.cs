using UnityEngine;

/// <summary>
/// Handles player footstep audio.
/// Footstep sounds are triggered by Animation Events on the walk/run clips:
///   Walk animation -> calls PlayWalkStep()
///   Run animation  -> calls PlayRunStep()
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

    private AudioSource _audioSource;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake  = false;
    }

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
