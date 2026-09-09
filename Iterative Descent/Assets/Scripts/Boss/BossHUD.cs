using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Boss health bar HUD. Lives on its own Canvas (separate from the player HUD Canvas).
///
/// Unity Setup:
///   1. Create a new Canvas GameObject named "BossHUDCanvas" (Screen Space - Overlay).
///   2. Add a background panel and a fill Image (Type: Filled, Fill Method: Horizontal).
///   3. Attach this script to BossHUDCanvas.
///   4. Wire the fields in the Inspector.
///   5. The Canvas starts INACTIVE -- call ShowHUD() from BossEncounterTrigger when the fight begins.
///
/// Events consumed:
///   BossHealth.OnHealthChanged
///   BossHealth.OnPhaseTwo
///   BossHealth.OnBossDeath
/// </summary>
public class BossHUD : MonoBehaviour
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Health Bar")]
    [Tooltip("Image component with Image Type = Filled, Fill Method = Horizontal.")]
    public Image healthFillImage;

    [Header("Boss Name")]
    [Tooltip("Label showing the boss name. Set the text in the Inspector or leave blank.")]
    public TextMeshProUGUI bossNameText;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    // Cached in Awake() so HandleHealthReset() below has the un-tinted color to restore --
    // HandlePhaseTwo() overwrites healthFillImage.color permanently otherwise.
    private Color _defaultFillColor = Color.white;

    void Awake()
    {
        if (healthFillImage != null)
            _defaultFillColor = healthFillImage.color;

        BossHealth.OnHealthChanged += HandleHealthChanged;
        BossHealth.OnPhaseTwo      += HandlePhaseTwo;
        BossHealth.OnBossDeath     += HandleBossDeath;
        BossHealth.OnHealthReset   += HandleHealthReset;
    }

    void OnDestroy()
    {
        BossHealth.OnHealthChanged -= HandleHealthChanged;
        BossHealth.OnPhaseTwo      -= HandlePhaseTwo;
        BossHealth.OnBossDeath     -= HandleBossDeath;
        BossHealth.OnHealthReset   -= HandleHealthReset;
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Show the HUD Canvas. Call from BossEncounterTrigger when the fight starts.</summary>
    public void ShowHUD()
    {
        gameObject.SetActive(true);
    }

    /// <summary>Hide the HUD Canvas. Call after boss death sequence completes.</summary>
    public void HideHUD()
    {
        gameObject.SetActive(false);
    }

    [ContextMenu("Test: Show HUD")]
    void Debug_Show() => ShowHUD();

    [ContextMenu("Test: Hide HUD")]
    void Debug_Hide() => HideHUD();

    // ─── Event Handlers ─────────────────────────────────────────────────────────

    void HandleHealthChanged(float current, float max)
    {
        if (healthFillImage != null)
            healthFillImage.fillAmount = max > 0f ? current / max : 0f;
    }

    void HandlePhaseTwo()
    {
        // Tint the health bar red to signal phase 2.
        if (healthFillImage != null)
            healthFillImage.color = new Color(0.85f, 0.15f, 0.15f);

        Debug.Log("[BossHUD] Phase 2 visual triggered.");
    }

    void HandleBossDeath()
    {
        // Brief pause before hiding so the player sees the bar hit zero.
        Invoke(nameof(HideHUD), 2f);
    }

    // Undoes HandlePhaseTwo()'s permanent red tint on a checkpoint respawn -- without this,
    // a boss reset back to Phase 1 would still show the Phase 2 color on a full health bar.
    void HandleHealthReset()
    {
        if (healthFillImage != null)
            healthFillImage.color = _defaultFillColor;
    }
}
