using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to the Death Screen Canvas (or a child panel).
/// Listens to PlayerHealth.OnPlayerDied, shows itself, and handles restart/quit.
///
/// Unity Setup:
///   - Set the Canvas/Panel inactive by default in the Inspector.
///   - Wire RestartGame() to your Restart button's On Click ().
///   - Wire QuitToMenu() to your Main Menu button's On Click () (optional).
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [Header("Death Screen Panel")]
    [Tooltip("Root panel to show on death. Assign if this script is not on the panel itself.")]
    [SerializeField] private GameObject deathPanel;

    void Awake()
    {
        // If no panel assigned, treat this GameObject as the panel.
        if (deathPanel == null)
            deathPanel = gameObject;

        deathPanel.SetActive(false);
    }

    void OnEnable()
    {
        PlayerHealth.OnPlayerDied     += ShowDeathScreen;
        PlayerHealth.OnPlayerRespawned += HideDeathScreen;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerDied     -= ShowDeathScreen;
        PlayerHealth.OnPlayerRespawned -= HideDeathScreen;
    }

    void ShowDeathScreen()
    {
        Time.timeScale = 0f;
        deathPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    /// <summary>
    /// Fires when CheckpointManager.RespawnPlayer() revives the player (see
    /// PlayerHealth.Revive() -> OnPlayerRespawned). Only relevant after RestartGame()
    /// below has chosen the respawn branch -- hides this panel and unfreezes time so
    /// the player is dropped straight back into a playable state at the checkpoint.
    /// </summary>
    void HideDeathScreen()
    {
        Time.timeScale = 1f;
        deathPanel.SetActive(false);
    }

    // ─── Button Callbacks ────────────────────────────────────────────────────────

    /// <summary>
    /// Wired to the death screen's Restart/Respawn button. If a checkpoint has been
    /// reached, respawns the player in place there instead of reloading the scene --
    /// HideDeathScreen() above (via PlayerHealth.OnPlayerRespawned) takes care of
    /// hiding this panel once that happens. Otherwise (no checkpoint yet -- e.g. dying
    /// in the very first encounter) falls back to the original full scene reload.
    /// </summary>
    public void RestartGame()
    {
        if (CheckpointManager.Instance != null && CheckpointManager.Instance.HasCheckpoint)
        {
            CheckpointManager.Instance.RespawnPlayer();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0); // change index if your main menu is not scene 0
    }
}
