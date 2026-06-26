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
        PlayerHealth.OnPlayerDied += ShowDeathScreen;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerDied -= ShowDeathScreen;
    }

    void ShowDeathScreen()
    {
        Time.timeScale = 0f;
        deathPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ─── Button Callbacks ────────────────────────────────────────────────────────

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0); // change index if your main menu is not scene 0
    }
}
