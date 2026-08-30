// PlayerInteractor.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    // ── Singleton instance (used by HelpPanelUI to detect scene context) ──
    public static PlayerInteractor Instance { get; private set; }

    // ── Pause flag — set by PuzzleProp when UI is open ───────────
    private static bool _paused = false;
    public static bool IsPaused => _paused;
    public static void Pause()
    {
        _paused = true;
        // Update() returns early while paused and never calls UpdateHint() again,
        // so without this the cycle hint panel freezes visible if it happened to
        // be showing the instant an overlay opened, bleeding through behind it.
        SelectionPromptUI.Instance?.Hide();
    }
    public static void Resume() => _paused = false;

    // ── Active closeable — the currently-open puzzle overlay ─────
    // Registered by each puzzle prop when it opens; cleared on close.
    // PlayerInteractor routes ESC to it while paused so the overlay
    // can dismiss itself without every UI needing its own ESC check.
    private static ICloseable _activeCloseable;
    public static void RegisterCloseable(ICloseable c)   => _activeCloseable = c;
    public static void DeregisterCloseable()             => _activeCloseable = null;

    // Set to true the frame ESC was consumed by a puzzle close.
    // PauseMenuUI reads this to avoid immediately re-opening.
    private static bool _escConsumedThisFrame;
    public  static bool EscConsumedThisFrame => _escConsumedThisFrame;

    // ── Static registry ──────────────────────────────────────────
    private static readonly List<InteractableBase> _all = new();
    public static void Register(InteractableBase obj) => _all.Add(obj);
    public static void Unregister(InteractableBase obj) => _all.Remove(obj);

    // ── Runtime ──────────────────────────────────────────────────
    private readonly List<InteractableBase> _inRange = new();
    private int _selectedIndex = 0;

    [Header("Line of Sight")]
    [Tooltip("Eye height (relative to the player's own transform) the obstruction raycast is cast from/to. Matches the convention used by EnemyChaser's proximity LOS check.")]
    public float losHeight = 1.6f;

    // Obstruction layer for the line-of-sight check -- same layer name used by
    // EnemyChaser's HasLineOfSight() and the Boss attack wall checks, so a
    // single "Level 1 Obstacle" layer governs LOS project-wide.
    private int _wallLayer;

    void Awake()
    {
        Instance = this;
        _wallLayer = LayerMask.GetMask("Level 1 Obstacle");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        // Reset the consumed flag each frame so PauseMenuUI gets a clean read.
        _escConsumedThisFrame = false;
    }

    void Update()
    {
        // While paused, only handle ESC to close the active overlay.
        if (_paused)
        {
            if (_activeCloseable != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _activeCloseable.Close();
                _escConsumedThisFrame = true;
            }
            return;
        }

        RefreshInRange();
        HandleSelection();
        UpdateAllStates();
        HandleInteract();
    }

    void RefreshInRange()
    {
        _inRange.Clear();
        foreach (var obj in _all)
        {
            if (obj == null) continue;
            if (Vector3.Distance(transform.position, obj.Center) <= obj.highlightRadius
                && HasLineOfSight(obj))
                _inRange.Add(obj);
        }

        _inRange.Sort((a, b) =>
            Vector3.Distance(transform.position, a.Center)
            .CompareTo(Vector3.Distance(transform.position, b.Center)));

        if (_inRange.Count == 0) _selectedIndex = 0;
        else _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _inRange.Count - 1);
    }

    /// <summary>
    /// Raycasts between the player and the prop's interaction point at
    /// losHeight, against the "Level 1 Obstacle" layer. Returns true (clear)
    /// if nothing on that layer is between them -- so a prop behind a wall
    /// stays out of _inRange even when it's within highlightRadius.
    /// </summary>
    private bool HasLineOfSight(InteractableBase obj)
    {
        Vector3 from = transform.position + Vector3.up * losHeight;
        Vector3 to   = obj.Center;

        // Linecast returns true if it HIT something -- i.e. blocked -- so a
        // clear line of sight is the inverse of that.
        return !Physics.Linecast(from, to, _wallLayer, QueryTriggerInteraction.Ignore);
    }

    void HandleSelection()
    {
        if (_inRange.Count <= 1) return;

        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            _selectedIndex = (_selectedIndex + 1) % _inRange.Count;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            _selectedIndex = (_selectedIndex - 1 + _inRange.Count) % _inRange.Count;
    }

    void UpdateAllStates()
    {
        foreach (var obj in _all)
        {
            if (obj == null) continue;
            if (!_inRange.Contains(obj))
                obj.UpdateState(false, false);
        }

        for (int i = 0; i < _inRange.Count; i++)
            _inRange[i].UpdateState(true, i == _selectedIndex);

        if (SelectionPromptUI.Instance != null)
            SelectionPromptUI.Instance.UpdateHint(_inRange, _selectedIndex);
    }

    void HandleInteract()
    {
        if (_inRange.Count == 0) return;
        if (!Keyboard.current.eKey.wasPressedThisFrame) return;
        _inRange[_selectedIndex].TryInteract(gameObject);
    }
}