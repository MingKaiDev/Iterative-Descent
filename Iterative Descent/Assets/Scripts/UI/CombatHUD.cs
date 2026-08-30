using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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
    [Tooltip("Assign the ReticleController component (on the Reticle Canvas child). " +
             "It manages its own visibility -- do not assign a plain crosshair here.")]
    public ReticleController reticle;

    [Header("Puzzle Visibility")]
    [Tooltip("Parent GameObject containing the health bar and ammo display. " +
             "Hidden automatically whenever a puzzle UI is open. " +
             "Create an empty Canvas child called 'CombatHUD Panel', reparent health and ammo elements into it, then assign it here.")]
    public GameObject combatHUDPanel;

    [Header("Death Screen")]
    [Tooltip("Full-screen panel shown on player death. " +
             "Create a Canvas child panel with a YOU DIED text and a Restart button. " +
             "The button's OnClick should call CombatHUD.RestartScene().")]
    public GameObject deathOverlay;

    // ─── Private ────────────────────────────────────────────────────────────────
    private PlayerCombat      _combat;
    private ShotgunController _shotgun;
    private RifleController   _rifle;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        PlayerHealth.OnHealthChanged    += HandleHealthChanged;
        PlayerCombat.OnAmmoChanged      += HandlePistolAmmoChanged;
        ShotgunController.OnAmmoChanged += HandleShotgunAmmoChanged;
        RifleController.OnAmmoChanged   += HandleRifleAmmoChanged;
        PlayerHealth.OnPlayerDied       += HandlePlayerDied;

        // Death overlay starts hidden.
        if (deathOverlay != null)
            deathOverlay.SetActive(false);

        // Reticle manages its own visibility via CanvasGroup alpha.
    }

    void OnDestroy()
    {
        PlayerHealth.OnHealthChanged    -= HandleHealthChanged;
        PlayerCombat.OnAmmoChanged      -= HandlePistolAmmoChanged;
        ShotgunController.OnAmmoChanged -= HandleShotgunAmmoChanged;
        RifleController.OnAmmoChanged   -= HandleRifleAmmoChanged;
        PlayerHealth.OnPlayerDied       -= HandlePlayerDied;
    }

    void Start()
    {
        _combat  = FindObjectOfType<PlayerCombat>();
        _shotgun = FindObjectOfType<ShotgunController>();
        _rifle   = FindObjectOfType<RifleController>();

        // Seed ammo display in case events fire before this Start()
        if (_combat != null)
            HandlePistolAmmoChanged(_combat.CurrentMag, _combat.SpareAmmo);
    }

    void Update()
    {
        // Hide health/ammo panel while any puzzle UI is open.
        if (combatHUDPanel != null)
            combatHUDPanel.SetActive(!PlayerInteractor.IsPaused);

        // Reticle self-manages visibility via ReticleController.Update().
    }

    // ─── Event Handlers ─────────────────────────────────────────────────────────

    void HandleHealthChanged(float current, float max)
    {
        if (healthFillImage != null)
            healthFillImage.fillAmount = max > 0f ? current / max : 0f;

        if (healthValueText != null)
            healthValueText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    void HandlePistolAmmoChanged(int mag, int spare)
    {
        // Ignore if pistol is not the active weapon.
        if (_combat != null && !_combat.enabled) return;
        if (ammoText != null) ammoText.text = $"[ {mag} | {spare} ]";
    }

    void HandleShotgunAmmoChanged(int mag, int spare)
    {
        // Ignore if shotgun is not the active weapon.
        if (_shotgun != null && !_shotgun.enabled) return;
        if (ammoText != null) ammoText.text = $"[ {mag} | {spare} ]";
    }

    void HandleRifleAmmoChanged(int mag, int spare)
    {
        // Ignore if rifle is not the active weapon.
        if (_rifle != null && !_rifle.enabled) return;
        if (ammoText != null) ammoText.text = $"[ {mag} | {spare} ]";
    }

    // ─── Death ──────────────────────────────────────────────────────────────────

    void HandlePlayerDied()
    {
        if (deathOverlay != null)
            deathOverlay.SetActive(true);
        // Cursor is already unlocked by PlayerMovement.HandlePlayerDied().
    }

    /// <summary>
    /// Assign to the Restart button's OnClick event in the Inspector.
    /// Reloads the current scene from the beginning.
    /// </summary>
    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
