// OnboardingHintUI.cs
// Brief, non-blocking on-screen control hints for first-time players (Story 31).
//
// Two independent one-shot hints, each shown exactly once per session:
//   - Movement hint: shown the moment the player spawns (PlayerMovement.OnPlayerSpawned).
//     Auto-dismisses the instant the player presses a movement key, or after
//     movementHintTimeout seconds if they never do.
//   - Combat hint: shown the first time ANY scripted encounter activates at least
//     one enemy (see EncounterTrigger.ActivateEncounter). Auto-dismisses after
//     combatHintDuration seconds. Does not care which room/encounter fires first --
//     whichever the player reaches first triggers it, exactly once.
//
// This is deliberately NOT an ICloseable overlay. It never pauses the game, never
// unlocks the cursor, and never registers with PlayerInteractor as the active
// closeable. It is a passive HUD label, same visibility tier as CombatHUD's health
// bar, so it should live under the same Canvas and simply auto-hide while any real
// puzzle/dialogue overlay has the game paused (PlayerInteractor.IsPaused), so it
// never bleeds through behind a paused overlay.
//
// SCENE SETUP -- see FYP/setup-guides/OnboardingHint_UnitySetup.md
//
// Text is ASCII-only by project convention (no Unicode arrows/dashes/emoji --
// see CLAUDE.md and Story 28 notes). Uses "|" as a separator, not an em dash.

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class OnboardingHintUI : MonoBehaviour
{
    public static OnboardingHintUI Instance { get; private set; }

    [Header("Panel")]
    [Tooltip("TMP label that displays whichever hint is currently active.")]
    [SerializeField] private TMP_Text hintLabel;

    [Header("Movement Hint")]
    [TextArea(1, 2)]
    [SerializeField] private string movementHintText = "WASD Move | Mouse Look | E Interact | ESC Pause";
    [Tooltip("Auto-hide the movement hint after this many seconds even if the player never moves.")]
    [SerializeField] private float movementHintTimeout = 12f;

    [Header("Combat Hint")]
    [TextArea(1, 2)]
    [SerializeField] private string combatHintText = "RMB Aim | LMB Shoot | R Reload | 1/2 Swap Weapon";
    [SerializeField] private float combatHintDuration = 6f;

    // ── Per-hint one-shot + timer state ──────────────────────────────────
    private bool  _movementHintShown;
    private bool  _movementHintActive;
    private float _movementHintTimer;

    private bool  _combatHintShown;
    private bool  _combatHintActive;
    private float _combatHintTimer;

    void Awake()
    {
        Instance = this;

        // Do NOT SetActive(false) on this GameObject -- see project gotcha #9
        // (feedback-unity-gotchas.md): a GameObject that starts inactive defers
        // Awake()/OnEnable() until something explicitly activates it, but the
        // only thing that would do that here is the spawn-event handler
        // registered inside OnEnable() below -- a deadlock. This GameObject
        // must stay active in the Inspector at all times; only the child
        // label is toggled, in Display()/Update() below.
        if (hintLabel != null) hintLabel.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        PlayerMovement.OnPlayerSpawned += HandlePlayerSpawned;
    }

    void OnDisable()
    {
        PlayerMovement.OnPlayerSpawned -= HandlePlayerSpawned;
    }

    void HandlePlayerSpawned() => ShowMovementHint();

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Shows the movement/interact hint. No-op after the first call this session.</summary>
    public void ShowMovementHint()
    {
        if (_movementHintShown) return;
        _movementHintShown  = true;
        _movementHintActive = true;
        _movementHintTimer  = 0f;
        Display(movementHintText);
    }

    /// <summary>
    /// Shows the combat control hint. Call this from any encounter's activation path --
    /// it is safe to call every time an encounter fires; only the first call this
    /// session actually shows anything.
    /// </summary>
    public void ShowCombatHintOnce()
    {
        if (_combatHintShown) return;
        _combatHintShown  = true;
        _combatHintActive = true;
        _combatHintTimer  = 0f;
        Display(combatHintText);
    }

    // ── Internals ────────────────────────────────────────────────────────

    void Display(string text)
    {
        if (hintLabel == null) return;
        hintLabel.gameObject.SetActive(true);
        hintLabel.text = text;
    }

    void Update()
    {
        // Hide (but keep counting nothing) while any puzzle/dialogue overlay
        // has paused the game -- avoids bleeding through behind it.
        // Only the label GameObject is toggled -- this script's own
        // GameObject must never be deactivated (see Awake() note).
        if (PlayerInteractor.IsPaused)
        {
            if (hintLabel != null) hintLabel.gameObject.SetActive(false);
            return;
        }

        if (_movementHintActive)
        {
            _movementHintTimer += Time.deltaTime;
            if (HasMovementInput() || _movementHintTimer >= movementHintTimeout)
                _movementHintActive = false;
        }

        if (_combatHintActive)
        {
            _combatHintTimer += Time.deltaTime;
            if (_combatHintTimer >= combatHintDuration)
                _combatHintActive = false;
        }

        bool anyActive = _movementHintActive || _combatHintActive;
        if (!anyActive)
        {
            if (hintLabel != null) hintLabel.gameObject.SetActive(false);
            return;
        }

        if (hintLabel != null)
        {
            hintLabel.gameObject.SetActive(true);
            // If both were ever active at once, combat hint text wins (more urgent).
            hintLabel.text = _combatHintActive ? combatHintText : movementHintText;
        }
    }

    bool HasMovementInput()
    {
        if (Keyboard.current == null) return false;
        return Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed ||
               Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed;
    }
}
