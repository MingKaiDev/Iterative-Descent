using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural animated water fill for the Drain puzzle panel.
///
/// Draws a filled water body from the bottom of the panel up to a
/// sine-wave surface. The wave amplitude scales with urgency (high
/// flood level = violent waves). As the player succeeds, fill drops
/// toward 0 and waves calm down.
///
/// SETUP
/// -----
///   1. Add an empty child GameObject to DrainPuzzlePanel -- call it WaterFill.
///   2. Move it to the TOP of the hierarchy (renders first = behind content).
///   3. Attach this script to it.
///   4. Set RectTransform: Anchor min (0,0) max (1,1), all offsets 0 (stretch to panel).
///   5. Drag it into DrainPuzzleUI.waterFill in the Inspector.
///
/// Call SetFillLevel(0..1) from DrainPuzzleUI whenever puzzle state changes.
///   0 = empty (drain fully cleared, player won)
///   1 = completely flooded
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class DrainWaterFill : MaskableGraphic
{
    [Header("Fill")]
    [Range(0f, 1f)]
    [Tooltip("Starting fill level set via Inspector for preview; overwritten at runtime.")]
    public float fillLevel = 0.65f;

    [Header("Wave")]
    [Tooltip("Max height of a wave crest in pixels at full urgency.")]
    public float waveAmplitude = 10f;
    [Tooltip("Number of full wave cycles across the panel width.")]
    public float waveFrequency = 1.8f;
    [Tooltip("How fast the wave scrolls (unscaled time, flood-immune).")]
    public float waveSpeed = 1.4f;

    [Header("Colors")]
    [Tooltip("Water body fill -- dark indigo so it reads as water against the red panel.")]
    public Color waterBodyColor    = new Color(0.04f, 0.02f, 0.22f, 0.28f);
    [Tooltip("Wave surface line -- slightly brighter.")]
    public Color waveSurfaceColor  = new Color(0.08f, 0.04f, 0.50f, 0.48f);

    // -------------------------------------------------------------------------

    private float _waveOffset;
    private float _currentFill;
    private float _targetFill;
    private float _urgency;

    private const int   WaveSteps     = 48;
    private const float FillLerpSpeed = 1.8f;
    private const float UrgencySpeed  = 2.2f;

    protected override void Awake()
    {
        base.Awake();
        _currentFill = fillLevel;
        _targetFill  = fillLevel;
        _urgency     = fillLevel;
        raycastTarget = false; // never block clicks
    }

    void Update()
    {
        _waveOffset  += Time.unscaledDeltaTime * waveSpeed;
        _currentFill  = Mathf.Clamp01(Mathf.Lerp(_currentFill, _targetFill,  Time.unscaledDeltaTime * FillLerpSpeed));
        _urgency      = Mathf.Clamp01(Mathf.Lerp(_urgency,     _currentFill, Time.unscaledDeltaTime * UrgencySpeed));
        fillLevel     = _currentFill;
        SetVerticesDirty();
    }

    /// <summary>
    /// Drive the water level from DrainPuzzleUI.
    /// level 0 = dry (winning), level 1 = flooded.
    /// instant = true skips the lerp (use on puzzle reset).
    /// </summary>
    public void SetFillLevel(float level, bool instant = false)
    {
        _targetFill = Mathf.Clamp01(level);
        if (instant)
        {
            _currentFill = _targetFill;
            _urgency     = _targetFill;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_currentFill <= 0.002f) return;

        Rect  r    = rectTransform.rect;
        float w    = r.width;
        float h    = r.height;
        float xMin = r.xMin;
        float yMin = r.yMin;

        // Y of water surface measured from bottom
        float surfaceY = yMin + h * _currentFill;

        // Amplitude scales with urgency: calmer when water is low
        float amp = waveAmplitude * (0.3f + 0.7f * _urgency);

        float     stepW = w / WaveSteps;
        UIVertex  vert  = UIVertex.simpleVert;

        for (int i = 0; i <= WaveSteps; i++)
        {
            float x     = xMin + i * stepW;
            float phase = (x / w) * waveFrequency * Mathf.PI * 2f + _waveOffset;

            // Two sine waves at slightly different frequencies for organic look
            float waveY = surfaceY
                + Mathf.Sin(phase)              * amp
                + Mathf.Sin(phase * 1.73f + 1f) * amp * 0.35f;

            // Bottom vertex
            vert.position = new Vector3(x, yMin, 0);
            vert.color    = waterBodyColor;
            vh.AddVert(vert);

            // Surface vertex
            vert.position = new Vector3(x, waveY, 0);
            vert.color    = waveSurfaceColor;
            vh.AddVert(vert);
        }

        // Triangle strip
        for (int i = 0; i < WaveSteps; i++)
        {
            int bl = i * 2;
            int tl = i * 2 + 1;
            int br = i * 2 + 2;
            int tr = i * 2 + 3;
            vh.AddTriangle(bl, tl, tr);
            vh.AddTriangle(bl, tr, br);
        }
    }
}
