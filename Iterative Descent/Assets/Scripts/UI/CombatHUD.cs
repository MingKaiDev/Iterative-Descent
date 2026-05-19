using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Combat HUD — drives three elements:
///   • Health bar (top-left) — Image set to Filled / Horizontal
///   • Ammo display — TextMeshPro showing [ mag | spare ]
///   • Crosshair — any GameObject; active only when player is aiming
///
/// Wire everything up in the Inspector on the Canvas GameObject this script lives on.
/// No polling; all updates fire through static events from PlayerHealth and PlayerCombat.
/// </summary>
public class CombatHUD : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Health Bar — Top Left")]
    [Tooltip("Image component with Image Type set to Filled, Fill Method = Horizontal.")]
    public Image healthFillImage;
    [Tooltip("Optional label e.g. '75 / 100'. Leave blank to hide.")]
    public TextMeshProUGUI healthValueText;

    [Header("Ammo Display")]
    [Tooltip("TextMeshPro showing [ mag | spare ].")]
    public TextMeshProUGUI ammoText;

    [Header("Crosshair")]
    [Tooltip("GameObject to show only while the player is aiming. Assign a dot/crosshair image.")]
    public GameObject crosshair;

    // ─── Private ────────────────────────────────────────────────────────────────
    private PlayerCombat _combat;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        PlayerHealth.OnHealthChanged += HandleHealthChanged;
        PlayerCombat.OnAmmoChanged   += HandleAmmoChanged;
    }

    void OnDestroy()
    {
        PlayerHealth.OnHealthChanged -= HandleHealthChanged;
        PlayerCombat.OnAmmoChanged   -= HandleAmmoChanged;
    }

    void Start()
    {
        _combat = FindObjectOfType<PlayerCombat>();

        // Hide crosshair until we aim
        if (crosshair != null)
            crosshair.SetActive(false);

        // Seed ammo display in case events fire before this Start()
        if (_combat != null)
            HandleAmmoChanged(_combat.CurrentMag, _combat.SpareAmmo);
    }

    void Update()
    {
        // Crosshair tracks aiming state every frame (cheap bool check)
        if (crosshair != null && _combat != null)
            crosshair.SetActive(_combat.IsAiming && !_combat.IsReloading);
    }

    // ─── Event Handlers ─────────────────────────────────────────────────────────

    void HandleHealthChanged(float current, float max)
    {
        if (healthFillImage != null)
            healthFillImage.fillAmount = max > 0f ? current / max : 0f;

        if (healthValueText != null)
            healthValueText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    void HandleAmmoChanged(int mag, int spare)
    {
        if (ammoText != null)
            ammoText.text = $"[ {mag} | {spare} ]";
    }
}
