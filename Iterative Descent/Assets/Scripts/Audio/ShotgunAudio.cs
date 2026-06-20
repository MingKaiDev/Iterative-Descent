using UnityEngine;

/// <summary>
/// Procedurally generates and plays shotgun audio.
/// No audio files required -- all clips are synthesised at runtime.
///
/// Designed for a heavy, DOOM-style feel:
///   - Fire    : sub-bass punch (42 Hz) + pitch-dropping boom + saturated noise body (~0.7s)
///   - Reload  : subtle pull-back click then heavy CHUNK forward slam (~0.5s)
///   - DryFire : hollow metallic click (~0.14s)
///
/// Tuning tips (all in the static Build* methods):
///   satDrive  -- tanh saturation pre-gain. Higher = grittier / more distorted.
///   Sub freq  -- lower (35-45 Hz) = more bass thump; raise if it sounds muddy.
///   Boom freq sweep -- starts high, falls to ~45 Hz for that "dropping shell" feel.
///
/// Setup: Add to Player GameObject. AudioSource is added automatically.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ShotgunAudio : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Volume")]
    [Range(0f, 1f)] public float fireVolume    = 0.95f;
    [Range(0f, 1f)] public float reloadVolume  = 0.80f;
    [Range(0f, 1f)] public float dryFireVolume = 0.50f;

    // ─── Private ─────────────────────────────────────────────────────────────────

    private AudioSource _source;
    private AudioClip   _fireClip;
    private AudioClip   _reloadClip;
    private AudioClip   _dryFireClip;

    private const int SR = 44100;   // sample rate

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        _source              = GetComponent<AudioSource>();
        _source.playOnAwake  = false;
        _source.spatialBlend = 0f;   // 2D -- player's own weapon

        _fireClip    = BuildFireClip();
        _reloadClip  = BuildReloadClip();
        _dryFireClip = BuildDryFireClip();

        ShotgunController.OnFired       += PlayFire;
        ShotgunController.OnReloadStart += PlayReload;
        ShotgunController.OnDryFire     += PlayDryFire;
    }

    void OnDestroy()
    {
        ShotgunController.OnFired       -= PlayFire;
        ShotgunController.OnReloadStart -= PlayReload;
        ShotgunController.OnDryFire     -= PlayDryFire;
    }

    // ─── Playback ────────────────────────────────────────────────────────────────

    void PlayFire()    => _source.PlayOneShot(_fireClip,    fireVolume);
    void PlayReload()  => _source.PlayOneShot(_reloadClip,  reloadVolume);
    void PlayDryFire() => _source.PlayOneShot(_dryFireClip, dryFireVolume);

    // ─── Fire ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Layered blast with:
    ///   1. Sub punch     -- 42 Hz sine, fast-attack/slow-decay (the chest thump)
    ///   2. Pitch-drop boom -- sine sweeps 110 Hz -> 45 Hz over 0.3s (the "BOOM" falling away)
    ///   3. Initial crack -- full-white-noise burst, decays in ~15 ms (the bang transient)
    ///   4. Saturated body -- LP-filtered noise, mid decay (the gritty body)
    ///   5. Rumble tail   -- slow noise floor (the room air)
    ///   Then the whole signal is run through tanh saturation for harmonic grit.
    /// </summary>
    static AudioClip BuildFireClip()
    {
        float   duration = 0.70f;
        int     n        = Mathf.RoundToInt(duration * SR);
        float[] data     = new float[n];
        var     rng      = new System.Random(1337);

        float boomPhase = 0f;
        float lpState   = 0f;   // one-pole low-pass state for noise body

        for (int i = 0; i < n; i++)
        {
            float t     = (float)i / SR;
            float noise = Noise(rng);

            // 1. Sub punch: 42 Hz, long body, gives physical weight
            float sub   = Mathf.Sin(Mathf.PI * 2f * 42f * t) * Mathf.Exp(-t * 7f) * 1.3f;

            // 2. Pitch-dropping boom: starts at 110 Hz, falls to 45 Hz over 0.3s
            float boomFreq = Mathf.Lerp(110f, 45f, Mathf.Clamp01(t / 0.30f));
            boomPhase += boomFreq * (Mathf.PI * 2f) / SR;
            float boom  = Mathf.Sin(boomPhase) * Mathf.Exp(-t * 8f) * 1.1f;

            // 3. Initial crack: white noise burst (audible for only ~15 ms)
            float crack = noise * Mathf.Exp(-t * 220f) * 1.0f;

            // 4. LP-filtered noise body: one-pole filter (coeff 0.12 = mostly low-mid)
            //    Sounds like the compressed air and debris after the shot.
            lpState = lpState * 0.88f + noise * 0.12f;
            float body  = lpState * Mathf.Exp(-t * 5.5f) * 1.4f;

            // 5. Raw noise tail (rumble/room air), very quiet
            float tail  = noise * Mathf.Exp(-t * 3.5f) * 0.25f;

            float raw = sub + boom + crack + body + tail;

            // Saturate for grit -- tanh soft clipper at drive 2.6
            data[i] = Saturate(raw, 2.6f);
        }

        Normalize(data, 0.95f);
        return MakeClip("ShotgunFire", data);
    }

    // ─── Reload ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Two mechanical events:
    ///   Event 1 (t=0.00s)  -- subtle rearward pull: light click + high ring
    ///   Event 2 (t=0.22s)  -- forward CHUNK: heavy sub thud + metallic snap + ring
    /// The CHUNK is intentionally much heavier than the pull so the pump feels authoritative.
    /// </summary>
    static AudioClip BuildReloadClip()
    {
        float   duration = 0.50f;
        int     n        = Mathf.RoundToInt(duration * SR);
        float[] data     = new float[n];
        var     rng      = new System.Random(8888);

        for (int i = 0; i < n; i++)
        {
            float t     = (float)i / SR;
            float noise = Noise(rng);

            // ── Event 1: pull back (subtle) ────────────────────────────────────
            float e1Click = noise * Mathf.Exp(-t * 110f) * 0.40f;
            float e1Ring  = Mathf.Sin(Mathf.PI * 2f * 1800f * t) * Mathf.Exp(-t * 70f) * 0.18f;

            // ── Event 2: CHUNK forward (heavy) ─────────────────────────────────
            float t2 = t - 0.22f;
            float e2Sub   = t2 > 0f ? Mathf.Sin(Mathf.PI * 2f * 85f   * t2) * Mathf.Exp(-t2 * 38f) * 1.2f  : 0f;
            float e2Snap  = t2 > 0f ? noise * Mathf.Exp(-t2 * 95f) * 0.85f                                   : 0f;
            float e2Ring  = t2 > 0f ? Mathf.Sin(Mathf.PI * 2f * 720f  * t2) * Mathf.Exp(-t2 * 38f) * 0.35f : 0f;
            float e2Ring2 = t2 > 0f ? Mathf.Sin(Mathf.PI * 2f * 1450f * t2) * Mathf.Exp(-t2 * 55f) * 0.15f : 0f;

            float raw = e1Click + e1Ring + e2Sub + e2Snap + e2Ring + e2Ring2;

            // Lighter saturation -- we want the CHUNK to be clean-ish but meaty
            data[i] = Saturate(raw, 2.0f);
        }

        Normalize(data, 0.92f);
        return MakeClip("ShotgunReload", data);
    }

    // ─── Dry Fire ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hollow metallic snap of the firing pin on an empty chamber.
    /// Short but has a tiny sub thud so it doesn't feel weightless.
    /// </summary>
    static AudioClip BuildDryFireClip()
    {
        float   duration = 0.14f;
        int     n        = Mathf.RoundToInt(duration * SR);
        float[] data     = new float[n];
        var     rng      = new System.Random(42);

        for (int i = 0; i < n; i++)
        {
            float t     = (float)i / SR;
            float noise = Noise(rng);

            float click = noise * Mathf.Exp(-t * 160f) * 0.70f;
            float tone  = Mathf.Sin(Mathf.PI * 2f * 750f * t) * Mathf.Exp(-t * 70f) * 0.35f;
            float sub   = Mathf.Sin(Mathf.PI * 2f * 90f  * t) * Mathf.Exp(-t * 80f) * 0.30f;

            data[i] = Saturate(click + tone + sub, 1.8f);
        }

        Normalize(data, 0.80f);
        return MakeClip("ShotgunDryFire", data);
    }

    // ─── DSP Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Pade-approximant tanh soft clipper. drive > 1 = overdrive.
    /// drive 1.5 = light warmth, 2.5 = crunchy, 3.5+ = heavy distortion.
    /// </summary>
    static float Saturate(float x, float drive)
    {
        x *= drive;
        // Bounded Pade approximation of tanh -- fast and accurate for |x| < 4
        float x2 = x * x;
        float sat = x * (27f + x2) / (27f + 9f * x2);
        return Mathf.Clamp(sat, -1f, 1f);
    }

    static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

    static void Normalize(float[] data, float target)
    {
        float peak = 0f;
        foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        if (peak < 0.001f) return;
        float scale = target / peak;
        for (int i = 0; i < data.Length; i++)
            data[i] = Mathf.Clamp(data[i] * scale, -1f, 1f);
    }

    static AudioClip MakeClip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }
}
