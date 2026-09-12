// EndOfDemoUI.cs
// Simple "thanks for playing" panel shown after the boss dies and the front
// gate unlocks. Mirrors DeathScreenUI.cs's structure deliberately (panel
// SetActive toggle, pause timeScale, unlock cursor, SceneManager.LoadScene(0)
// for Main Menu) so the two screens behave consistently.
//
// 2026-09-12: gained a static Instance (see below) so BossDeathSequence can resolve
// this panel dynamically instead of relying only on a serialized Inspector reference --
// see the class doc comment on Instance for why a plain reference silently breaks
// specifically on a Level 1 -> Level 2 -> Level 1 round trip.
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
    // ── Instance (cross-scene resolution) ─────────────────────────────────────
    private static EndOfDemoUI _instance;

    /// <summary>
    /// Same pattern as BstPuzzleUI.Instance / SelectionPromptUI.Instance (see
    /// project-level-transition.md's "Root cause found + fixed: Instance is null on
    /// panels that start inactive"). Falls back to FindFirstObjectByType(...,
    /// FindObjectsInactive.Include) when the cached reference is null (Unity's
    /// overridden == also makes this true for a destroyed-but-not-yet-nulled object,
    /// which matters here specifically): this panel starts inactive by convention
    /// (see Awake()), so a fresh scene load may not have run Awake() on it yet the
    /// first time something needs it, and the panel this project actually keeps alive
    /// across a Level 1 -> Level 2 -> Level 1 round trip is the ORIGINAL one made
    /// DontDestroyOnLoad via PersistentUIRoot on the Canvas -- every later, freshly
    /// re-instantiated Level 1 copy gets destroyed by PersistentUIRoot's own dedupe
    /// guard moments after being created. A plain serialized Inspector reference (what
    /// BossDeathSequence used before this fix) gets wired to whichever copy exists at
    /// THAT load's scene-deserialize time -- fine on the very first Level 1 load, but
    /// pointing at an already-destroyed object after the return trip. This lookup
    /// always resolves to the one surviving instance instead.
    /// </summary>
    public static EndOfDemoUI Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<EndOfDemoUI>(FindObjectsInactive.Include);
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("End Of Demo Panel")]
    [Tooltip("Root panel to show. Assign if this script is not on the panel itself.")]
    [SerializeField] private GameObject panel;

    private void Awake()
    {
        Instance = this;

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
