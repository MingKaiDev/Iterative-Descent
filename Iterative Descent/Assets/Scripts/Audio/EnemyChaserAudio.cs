using UnityEngine;

/// <summary>
/// Handles audio for EnemyChaser: footsteps, hit reaction, and death sound.
/// Attach to the same root GameObject as EnemyChaser.
///
/// Footsteps are driven by Animation Events on the enemy walk clip:
///   Walk animation -> calls PlayWalkStep()
///
/// Hit sound is called directly by EnemyChaser.OnHit() via GetComponent,
/// only when the hit does not kill the enemy (see EnemyChaser.OnHit).
///
/// Death sound and pre-death sound are both called from EnemyChaser.OnDie()
/// via GetComponent, and play together (PlayOneShot allows overlap on one
/// AudioSource, so no coroutine/sequencing is needed).
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

    [Tooltip("Drag Enemy Hit clip here. Plays when the enemy takes damage " +
             "and survives -- suppressed on the killing hit so it doesn't " +
             "overlap the death sounds.")]
    public AudioClip hitClip;

    [Tooltip("Drag Enemy Pre-Death clip here. Plays at the same time as " +
             "Death Clip when the enemy dies.")]
    public AudioClip preDeathClip;

    [Tooltip("Drag Enemy Death clip here.")]
    public AudioClip deathClip;

    [Range(0f, 1f)]
    public float walkStepVolume = 0.7f;
    [Range(0f, 1f)]
    public float hitVolume      = 0.8f;
    [Range(0f, 1f)]
    public float preDeathVolume = 1f;
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
    /// Called from EnemyChaser.OnHit(), only when the hit does not kill the
    /// enemy (EnemyChaser checks _currentHealth before calling this).
    /// </summary>
    public void PlayHit()
    {
        if (hitClip == null)
        {
            Debug.LogWarning("[EnemyChaserAudio] No hit clip assigned.");
            return;
        }
        _audioSource.PlayOneShot(hitClip, hitVolume);
    }

    /// <summary>
    /// Called from EnemyChaser.OnDie(). Plays the pre-death and death clips
    /// together at the enemy's current world position even though the
    /// GameObject is about to be destroyed. PlayOneShot supports overlapping
    /// clips on a single AudioSource, so both fire back to back with no
    /// audible gap and no coroutine needed.
    /// </summary>
    public void PlayDeath()
    {
        if (preDeathClip != null)
            _audioSource.PlayOneShot(preDeathClip, preDeathVolume);

        if (deathClip == null)
        {
            Debug.LogWarning("[EnemyChaserAudio] No death clip assigned.");
            return;
        }
        // PlayOneShot at this position -- even if Destroy(gameObject, 3f) fires
        // shortly after, the AudioSource component is still alive long enough
        // for the clip(s) to finish (assuming clip < 3s; increase delay if needed).
        _audioSource.PlayOneShot(deathClip, deathVolume);
    }
}
