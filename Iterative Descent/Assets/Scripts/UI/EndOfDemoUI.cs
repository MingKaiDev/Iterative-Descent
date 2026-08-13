// EndOfDemoUI.cs
// Simple "thanks for playing" panel shown after the boss dies and the front
// gate unlocks. Mirrors DeathScreenUI.cs's structure deliberately (panel
// SetActive toggle, pause timeScale, unlock cursor, SceneManager.LoadScene(0)
// for Main Menu) so the two screens behave consistently.
//
// Unity Setup:
//   - Create an "End Of Demo Canvas" (or a panel on an existing overlay Canvas),
//     inactive by default.
//   - Attach this script to the Canvas/panel.
//   - Wire ReturnToMenu() to a "Return to Menu" button's On Click().
//   - Called by BossDeathSequence.Show() once the death sequence finishes --
//     do not call Show() from anywhere else without checking BossDeathSequence's
//     timing first.
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndOfDemoUI : MonoBehaviour
{
    [Header("End Of Demo Panel")]
    [Tooltip("Root panel to show. Assign if this script is not on the panel itself.")]
    [SerializeField] private GameObject panel;

    private void Awake()
    {
        if (panel == null)
            panel = gameObject;

        panel.SetActive(false);
    }

    public void Show()
    {
        Time.timeScale = 0f;
        panel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ─── Button Callback ─────────────────────────────────────────────────────────

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0); // Main Menu -- same index convention as DeathScreenUI.QuitToMenu()
    }
}
