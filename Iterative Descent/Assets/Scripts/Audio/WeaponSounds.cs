using UnityEngine;

/// <summary>
/// Handles all weapon audio for pistol, shotgun, and rifle.
/// Assign AudioClip assets in the Inspector -- no audio is generated at runtime.
///
/// Setup: Add to the Player GameObject alongside PlayerAudioController.
/// Events from an inactive weapon are ignored, so swapping weapons works automatically.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WeaponSounds : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Pistol")]
    public AudioClip pistolFireClip;
    public AudioClip pistolReloadClip;
    public AudioClip pistolDryFireClip;
    [Range(0f, 1f)] public float pistolFireVolume   = 0.9f;
    [Range(0f, 1f)] public float pistolReloadVolume = 0.8f;
    [Range(0f, 1f)] public float pistolDryVolume    = 0.5f;

    [Header("Shotgun")]
    public AudioClip shotgunFireClip;
    public AudioClip shotgunReloadClip;
    public AudioClip shotgunMagFullClip;   // played when magazine reaches max capacity
    public AudioClip shotgunDryFireClip;
    [Range(0f, 1f)] public float shotgunFireVolume    = 0.95f;
    [Range(0f, 1f)] public float shotgunReloadVolume  = 0.85f;
    [Range(0f, 1f)] public float shotgunMagFullVolume = 0.80f;
    [Range(0f, 1f)] public float shotgunDryVolume     = 0.5f;

    [Header("Rifle")]
    public AudioClip rifleFireClip;
    public AudioClip rifleReloadClip;
    public AudioClip rifleDryFireClip;
    public AudioClip rifleCockClip;        // bolt-cycle sound, played a short delay after firing
    public AudioClip rifleMagFullClip;     // played when magazine reaches max capacity
    [Range(0f, 1f)] public float rifleFireVolume    = 0.95f;
    [Range(0f, 1f)] public float rifleReloadVolume  = 0.85f;
    [Range(0f, 1f)] public float rifleDryVolume     = 0.5f;
    [Range(0f, 1f)] public float rifleCockVolume    = 0.75f;
    [Range(0f, 1f)] public float rifleMagFullVolume = 0.80f;

    // ─── Private ─────────────────────────────────────────────────────────────────

    private AudioSource       _source;
    private PlayerCombat      _pistol;
    private ShotgunController _shotgun;
    private RifleController   _rifle;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _source              = GetComponent<AudioSource>();
        _source.playOnAwake  = false;
        _source.spatialBlend = 0f;

        _pistol  = GetComponent<PlayerCombat>();
        _shotgun = GetComponent<ShotgunController>();
        _rifle   = GetComponent<RifleController>();

        PlayerCombat.OnFired         += OnPistolFired;
        PlayerCombat.OnReloadStart   += OnPistolReload;
        PlayerCombat.OnDryFire       += OnPistolDryFire;

        ShotgunController.OnFired       += OnShotgunFired;
        ShotgunController.OnReloadStart += OnShotgunReload;
        ShotgunController.OnMagFull     += OnShotgunMagFull;
        ShotgunController.OnDryFire     += OnShotgunDryFire;

        RifleController.OnFired       += OnRifleFired;
        RifleController.OnReloadStart += OnRifleReload;
        RifleController.OnDryFire     += OnRifleDryFire;
        RifleController.OnCocked      += OnRifleCock;
        RifleController.OnMagFull     += OnRifleMagFull;
    }

    void OnDestroy()
    {
        PlayerCombat.OnFired         -= OnPistolFired;
        PlayerCombat.OnReloadStart   -= OnPistolReload;
        PlayerCombat.OnDryFire       -= OnPistolDryFire;

        ShotgunController.OnFired       -= OnShotgunFired;
        ShotgunController.OnReloadStart -= OnShotgunReload;
        ShotgunController.OnMagFull     -= OnShotgunMagFull;
        ShotgunController.OnDryFire     -= OnShotgunDryFire;

        RifleController.OnFired       -= OnRifleFired;
        RifleController.OnReloadStart -= OnRifleReload;
        RifleController.OnDryFire     -= OnRifleDryFire;
        RifleController.OnCocked      -= OnRifleCock;
        RifleController.OnMagFull     -= OnRifleMagFull;
    }

    // ─── Pistol handlers ─────────────────────────────────────────────────────────

    void OnPistolFired()
    {
        if (_pistol != null && !_pistol.enabled) return;
        Play(pistolFireClip, pistolFireVolume);
    }

    void OnPistolReload()
    {
        if (_pistol != null && !_pistol.enabled) return;
        Play(pistolReloadClip, pistolReloadVolume);
    }

    void OnPistolDryFire()
    {
        if (_pistol != null && !_pistol.enabled) return;
        Play(pistolDryFireClip, pistolDryVolume);
    }

    // ─── Shotgun handlers ────────────────────────────────────────────────────────

    void OnShotgunFired()
    {
        if (_shotgun != null && !_shotgun.enabled) return;
        Play(shotgunFireClip, shotgunFireVolume);
    }

    void OnShotgunReload()
    {
        if (_shotgun != null && !_shotgun.enabled) return;
        Play(shotgunReloadClip, shotgunReloadVolume);
    }

    void OnShotgunMagFull()
    {
        if (_shotgun != null && !_shotgun.enabled) return;
        Play(shotgunMagFullClip, shotgunMagFullVolume);
    }

    void OnShotgunDryFire()
    {
        if (_shotgun != null && !_shotgun.enabled) return;
        Play(shotgunDryFireClip, shotgunDryVolume);
    }

    // ─── Rifle handlers ──────────────────────────────────────────────────────────

    void OnRifleFired()
    {
        if (_rifle != null && !_rifle.enabled) return;
        Play(rifleFireClip, rifleFireVolume);
    }

    void OnRifleReload()
    {
        if (_rifle != null && !_rifle.enabled) return;
        Play(rifleReloadClip, rifleReloadVolume);
    }

    void OnRifleCock()
    {
        if (_rifle != null && !_rifle.enabled) return;
        Play(rifleCockClip, rifleCockVolume);
    }

    void OnRifleMagFull()
    {
        if (_rifle != null && !_rifle.enabled) return;
        Play(rifleMagFullClip, rifleMagFullVolume);
    }

    void OnRifleDryFire()
    {
        if (_rifle != null && !_rifle.enabled) return;
        Play(rifleDryFireClip, rifleDryVolume);
    }

    // ─── Helper ──────────────────────────────────────────────────────────────────

    void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        _source.PlayOneShot(clip, volume);
    }
}
