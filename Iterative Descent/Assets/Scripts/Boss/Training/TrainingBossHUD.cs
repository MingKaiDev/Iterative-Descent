using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Training-scene boss HUD -- a smoothly-lerped health bar + name label for Training.unity,
/// separate from the production BossHUD.cs used in the live game. Recovered from an orphaned
/// missing-script reference on BossHUDRoot in Training.unity (2026-05-30 rollback); rebuilt to
/// match the exact field schema Unity had preserved (hudRoot, healthBarFill, bossNameText,
/// bossName, phase1Color, phase2Color, barLerpSpeed) so the scene's existing wiring and values
/// rebind automatically once this script's guid is repointed onto that slot.
///
/// Not used anywhere in the live game -- Level 1.unity has no BossHUDRoot/TrainingBossHUD.
///
/// Unity Setup:
///   - Attach to BossHUDRoot (the training HUD panel).
///   - hudRoot: usually this GameObject itself (used by Show/HideHUD instead of a hardcoded
///     gameObject reference, so a different root can be swapped in if the panel is ever split).
///   - healthBarFill: the Image (Filled, Horizontal) child showing HP.
///   - bossNameText: the TextMeshProUGUI child showing the boss name.
/// </summary>
public class TrainingBossHUD : MonoBehaviour
{
    // ─── Inspector: References ───────────────────────────────────────────────────
    [Header("References")]
    public GameObject hudRoot;
    public Image healthBarFill;
    public TextMeshProUGUI bossNameText;

    // ─── Inspector: Display ──────────────────────────────────────────────────────
    [Header("Display")]
    public string bossName = "ARES";
    public Color phase1Color = Color.white;
    public Color phase2Color = Color.red;
    [Tooltip("How fast (via Lerp, per second) the health bar chases its target fill amount.")]
    public float barLerpSpeed = 5f;

    // ─── Private ────────────────────────────────────────────────────────────────
    private float _targetFill = 1f;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        BossHealth.OnHealthChanged += HandleHealthChanged;
        BossHealth.OnPhaseTwo      += HandlePhaseTwo;
        BossHealth.OnBossDeath     += HandleBossDeath;
    }

    void OnDestroy()
    {
        BossHealth.OnHealthChanged -= HandleHealthChanged;
        BossHealth.OnPhaseTwo      -= HandlePhaseTwo;
        BossHealth.OnBossDeath     -= HandleBossDeath;
    }

    void Start()
    {
        if (bossNameText != null)
            bossNameText.text = bossName;

        if (healthBarFill != null)
        {
            healthBarFill.color = phase1Color;
            healthBarFill.fillAmount = _targetFill;
        }
    }

    void Update()
    {
        if (healthBarFill == null) return;
        healthBarFill.fillAmount = Mathf.Lerp(healthBarFill.fillAmount, _targetFill, barLerpSpeed * Time.deltaTime);
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    public void ShowHUD() => (hudRoot != null ? hudRoot : gameObject).SetActive(true);
    public void HideHUD() => (hudRoot != null ? hudRoot : gameObject).SetActive(false);

    /// <summary>Snaps the bar back to full and resets phase color. Call from BossTrainingEnv
    /// (Step 4) between episodes -- training resets far more often than the live game hides/shows.</summary>
    public void ResetVisual()
    {
        _targetFill = 1f;
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = 1f;
            healthBarFill.color = phase1Color;
        }
    }

    // ─── Event Handlers ─────────────────────────────────────────────────────────

    void HandleHealthChanged(float current, float max)
    {
        _targetFill = max > 0f ? current / max : 0f;
    }

    void HandlePhaseTwo()
    {
        if (healthBarFill != null)
            healthBarFill.color = phase2Color;
    }

    void HandleBossDeath()
    {
        // Training HUD does not auto-hide -- BossTrainingEnv resets visuals between episodes
        // instead of a one-shot hide like the live game's BossHUD.
    }
}
