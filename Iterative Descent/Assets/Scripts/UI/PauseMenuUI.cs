// PauseMenuUI.cs
// Singleton pause overlay toggled by ESC.
//
// SCENE SETUP
// -----------
//   1. Create a Canvas child called "Pause Panel" (inactive by default).
//   2. Attach this script to ANY scene GameObject (e.g. GameManager).
//   3. Assign pausePanel to that GameObject in the Inspector.
//   4. Add three Button children inside Pause Panel:
//        - resumeButton   -> calls PauseMenuUI.Instance.ClosePause()
//        - howToPlayButton -> calls HelpPanelUI.Instance.Show()
//        - quitButton     -> calls PauseMenuUI.Instance.QuitToMainMenu()
//   5. The Canvas must have a GraphicRaycaster; scene must have an EventSystem.
//
// BEHAVIOUR
// ----------
//   ESC opens the pause menu only when no puzzle overlay is active.
//   ESC (or Resume button) closes the pause menu.
//   The pause menu itself calls PlayerInteractor.Pause()/Resume() and manages
//   timeScale so combat, movement, and weapon input are all blocked while open.
//   PlayerInteractor.EscConsumedThisFrame prevents the same ESC press from
//   simultaneously closing a puzzle and reopening the pause menu.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    [Header("Panel")]
    [Tooltip("Root panel GameObject for the pause overlay (inactive by default).")]
    [SerializeField] private GameObject pausePanel;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button howToPlayButton;   // stub until Story 28
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ── Internal state ────────────────────────────────────────────
    private bool _isOpen;

    // ── Lifecycle ─────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void Start()
    {
        if (resumeButton  != null) resumeButton.onClick.AddListener(ClosePause);
        if (quitButton    != null) quitButton.onClick.AddListener(QuitToMainMenu);

        if (howToPlayButton != null)
            howToPlayButton.onClick.AddListener(() => HelpPanelUI.Instance?.Show());

        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => SettingsPanelUI.Instance?.Show());
    }

    void Update()
    {
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        // If PlayerInteractor already consumed this ESC (puzzle close), do nothing.
        if (PlayerInteractor.EscConsumedThisFrame) return;

        if (_isOpen)
            ClosePause();
        else if (!PlayerInteractor.IsPaused)
            OpenPause();
    }

    // ── Public API ────────────────────────────────────────────────

    public void OpenPause()
    {
        if (_isOpen) return;
        _isOpen = true;

        PlayerInteractor.Pause();
        Time.timeScale   = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void ClosePause()
    {
        if (!_isOpen) return;
        _isOpen = false;

        PlayerInteractor.Resume();
        Time.timeScale   = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void QuitToMainMenu()
    {
        // Restore game state before loading so the new scene starts clean.
        Time.timeScale   = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        PlayerInteractor.Resume();

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
