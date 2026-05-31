using UnityEngine;

/// <summary>
/// Handles audio for EnemyChaser: footsteps and death sound.
/// Attach to the same root GameObject as EnemyChaser.
///
/// Footsteps are driven by Animation Events on the enemy walk clip:
///   Walk animation -> calls PlayWalkStep()
///
/// Death sound is called directly by EnemyChaser.OnDie() via GetComponent.
///
/// Uses a 3D spatialBlend so the sound falls off with distance --
/// the player hears closer enemies more clearly.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class EnemyChaserAudio : MonoBehaviour
{
    [Header("Enemy SFX")]
    [Tooltip("Drag Enemy Walk footstep clip here.")]
    public AudioClip walkStepClip;

    [Tooltip("Drag Enemy Death clip here.")]
    public AudioClip deathClip;

    [Range(0f, 1f)]
    public float walkStepVolume = 0.7f;
    [Range(0f, 1f)]
    public float deathVolume    = 1f;

    // ─── Private ─────────────────────────────────────────────────────────────────

    private AudioSource _audioSource;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────────

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 1f;   // full 3D so distance attenuation applies
        _audioSource.playOnAwake  = false;
    }

    // ─── Animation Event Target ───────────────────────────────────────────────────

    /// <summary>
    /// Called by an Animation Event on the enemy walk clip at each footfall frame.
    /// </summary>
    public void PlayWalkStep()
    {
        if (walkStepClip == null)
        {
            Debug.LogWarning("[EnemyChaserAudio] No walk step clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(walkStepClip, walkStepVolume);
    }

    // ─── Called by EnemyChaser ────────────────────────────────────────────────────

    /// <summary>
    /// Called from EnemyChaser.OnDie(). Plays the death sound at the enemy's
    /// current world position even though the GameObject is about to be destroyed.
    /// </summary>
    public void PlayDeath()
    {
        if (deathClip == null)
        {
            Debug.LogWarning("[EnemyChaserAudio] No death clip assigned.");
            return;
        }
        // PlayOneShot at this position -- even if Destroy(gameObject, 3f) fires
        // shortly after, the AudioSource component is still alive long enough
        // for the clip to finish (assuming clip < 3s; increase delay if needed).
        _audioSource.PlayOneShot(deathClip, deathVolume);
    }
}
