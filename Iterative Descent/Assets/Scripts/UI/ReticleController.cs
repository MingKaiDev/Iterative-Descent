using UnityEngine;

/// <summary>
/// RE7-style 4-arm reticle. Arms start spread wide on fresh aim and converge
/// as AccuracyT rises, reaching minOffset once the player has held aim long enough.
///
/// Canvas Setup:
///   Create a Canvas child called "Reticle" (this GameObject).
///   Add 4 child Images named Top, Bottom, Left, Right.
///     Top/Bottom: ~20px wide, ~6px tall, anchored/pivoted to centre.
///     Left/Right: ~6px wide, ~20px tall, anchored/pivoted to centre.
///   Assign all 4 to this component in the Inspector.
///   Set this GO inactive in the Inspector -- ReticleController manages visibility.
///
/// Then assign this component to CombatHUD.reticle.
/// </summary>
public class ReticleController : MonoBehaviour
{
    [Header("Arms")]
    [Tooltip("RectTransform of the top arm Image.")]
    public RectTransform armTop;
    [Tooltip("RectTransform of the bottom arm Image.")]
    public RectTransform armBottom;
    [Tooltip("RectTransform of the left arm Image.")]
    public RectTransform armLeft;
    [Tooltip("RectTransform of the right arm Image.")]
    public RectTransform armRight;

    [Header("Spread")]
    [Tooltip("Arm offset in pixels when AccuracyT = 0 (fresh aim).")]
    public float maxOffset = 48f;
    [Tooltip("Arm offset in pixels when AccuracyT = 1 (fully settled).")]
    public float minOffset = 10f;

    // --- Private ------------------------------------------------------------
    private PlayerCombat      _pistol;
    private ShotgunController _shotgun;
    private RifleController   _rifle;
    private CanvasGroup       _canvasGroup;

    void Start()
    {
        _pistol  = FindObjectOfType<PlayerCombat>();
        _shotgun = FindObjectOfType<ShotgunController>();
        _rifle   = FindObjectOfType<RifleController>();

        // Use CanvasGroup alpha so this GO stays active and Update() always runs.
        _canvasGroup       = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup   = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
    }

    void Update()
    {
        bool  isAiming  = false;
        float accuracyT = 0f;

        if (_pistol != null && _pistol.enabled && _pistol.IsAiming)
        {
            isAiming  = true;
            accuracyT = _pistol.AccuracyT;
        }
        else if (_shotgun != null && _shotgun.enabled && _shotgun.IsAiming)
        {
            isAiming  = true;
            accuracyT = _shotgun.AccuracyT; // shotgun now has its own settle timer (spread capped at 50%, see ShotgunController)
        }
        else if (_rifle != null && _rifle.enabled && _rifle.IsAiming)
        {
            isAiming  = true;
            accuracyT = _rifle.AccuracyT; // rifle now has its own (slower) settle timer
        }

        bool shouldShow    = isAiming && !PlayerInteractor.IsPaused;
        _canvasGroup.alpha = shouldShow ? 1f : 0f;
        if (!shouldShow) return;

        // Directly mirror AccuracyT -- no extra smoothing so the visual
        // always matches actual accuracy (no lag after a shot resets the timer).
        float offset = Mathf.Lerp(maxOffset, minOffset, accuracyT);

        if (armTop    != null) armTop.anchoredPosition    = new Vector2(0f,     offset);
        if (armBottom != null) armBottom.anchoredPosition = new Vector2(0f,    -offset);
        if (armLeft   != null) armLeft.anchoredPosition   = new Vector2(-offset, 0f);
        if (armRight  != null) armRight.anchoredPosition  = new Vector2( offset, 0f);
    }

}
