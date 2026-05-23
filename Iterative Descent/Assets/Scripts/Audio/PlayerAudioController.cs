using UnityEngine;

/// <summary>
/// Handles all player weapon audio by listening to PlayerCombat's static events.
/// Attach this to the same GameObject as PlayerCombat (the Player prefab).
/// Uses PlayOneShot for the pistol shot so rapid fire never cuts off mid-sound.
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

    // ─── Private ────────────────────────────────────────────────────────────────
    private AudioSource _audioSource;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        // Configure: spatial 2D is fine for first-person-feel sounds on the player.
        // Set to 0 so the volume is consistent regardless of camera distance.
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake  = false;
    }

    void OnEnable()
    {
        PlayerCombat.OnFired         += HandleFired;
        PlayerCombat.OnReloadStart   += HandleReloadStart;
    }

    void OnDisable()
    {
        PlayerCombat.OnFired         -= HandleFired;
        PlayerCombat.OnReloadStart   -= HandleReloadStart;
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
}
