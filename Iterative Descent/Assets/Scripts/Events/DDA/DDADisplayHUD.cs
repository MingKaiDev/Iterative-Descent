using UnityEngine;

/// <summary>
/// In-editor and in-build DDA metrics HUD.
///
/// Draws an on-screen overlay showing:
///   - Current difficulty tier and score bar
///   - Raw vs smoothed score
///   - Per-signal breakdown (avg score / last time / pass rate)
///   - Live quiz metrics from PlayerMetricsTracker
///
/// Uses OnGUI so it works without any Canvas setup.
/// Toggle visibility with the H key (configurable below).
///
/// Attach anywhere — standalone GameObject, GameManager, whatever is convenient.
/// </summary>
public class DDADisplayHUD : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private KeyCode toggleKey = KeyCode.H;
    [SerializeField] private bool visibleByDefault = true;
    [SerializeField] private int panelWidth = 320;
    [SerializeField] private int panelX = 10;
    [SerializeField] private int panelY = 10;

    // Tier colour palette (Very Easy → Very Hard)
    private static readonly Color[] TierColours =
    {
        new Color(0.40f, 0.80f, 0.40f), // 0 Very Easy  — green
        new Color(0.60f, 0.85f, 0.40f), // 1 Easy       — yellow-green
        new Color(0.95f, 0.85f, 0.20f), // 2 Normal      — yellow
        new Color(0.95f, 0.55f, 0.15f), // 3 Hard        — orange
        new Color(0.90f, 0.25f, 0.25f), // 4 Very Hard   — red
    };

    private bool _visible;

    // Cached GUI styles (built once in first OnGUI call)
    private GUIStyle _boxStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _dimStyle;
    private bool _stylesReady;

    private void Awake()
    {
        _visible = visibleByDefault;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            _visible = !_visible;
    }

    private void OnGUI()
    {
        if (!_visible) return;

        DDAController dda = DDAController.Instance;
        PlayerMetricsTracker m = PlayerMetricsTracker.Instance;

        if (dda == null && m == null) return;

        BuildStyles();

        // ── Panel dimensions ───────────────────────────────────────────────
        int lineH = 20;
        int padV = 8;
        int lines = 24;
        int height = lines * lineH + padV * 2;

        Rect panel = new Rect(panelX, panelY, panelWidth, height);

        // Semi-transparent background
        Color prevColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(panel, GUIContent.none, _boxStyle);
        GUI.color = prevColor;

        GUILayout.BeginArea(new Rect(panel.x + 8, panel.y + padV, panel.width - 16, panel.height - padV));

        // ── Header ─────────────────────────────────────────────────────────
        GUILayout.Label("▐ DDA MONITOR", _headerStyle);
        HorizontalRule();

        if (dda != null)
        {
            // ── Tier badge ─────────────────────────────────────────────────
            int tier = Mathf.Clamp(dda.CurrentTier, 0, 4);
            GUI.color = TierColours[tier];
            GUILayout.Label($"  DIFFICULTY  →  {DDAController.TierNames[tier].ToUpper()}  (Tier {tier})", _headerStyle);
            GUI.color = Color.white;

            // ── Score bar ──────────────────────────────────────────────────
            GUILayout.Space(2);
            DrawBar("Difficulty Score", dda.CurrentScore, TierColours[tier]);
            GUILayout.Space(4);

            // ── Score numbers ──────────────────────────────────────────────
            GUILayout.Label($"  Smoothed Score : {dda.CurrentScore:F3}", _labelStyle);
            GUILayout.Label($"  Raw Score      : {dda.LastRawScore:F3}", _dimStyle);
            GUILayout.Label($"  Mode           : {(dda.AgentScoreOverride != null ? "PPO Agent" : "Heuristic")}", _dimStyle);
            HorizontalRule();

            // ── Signal breakdown ───────────────────────────────────────────
            GUILayout.Label("  SIGNAL BREAKDOWN", _dimStyle);
            DrawBar("  Avg Quiz Score  [55%]", dda.DbgAvgScoreSignal, Color.cyan);
            DrawBar("  Last Quiz Speed [25%]", dda.DbgLastTimeSignal, Color.yellow);
            DrawBar("  Pass Rate       [20%]", dda.DbgPassRateSignal, Color.green);
            HorizontalRule();
        }
        else
        {
            GUILayout.Label("  DDAController not found", _dimStyle);
            HorizontalRule();
        }

        // ── Live quiz metrics from tracker ─────────────────────────────────
        GUILayout.Label("  QUIZ METRICS", _dimStyle);
        if (m != null && m.TotalQuizAttempts > 0)
        {
            GUILayout.Label($"  Attempts       : {m.TotalQuizAttempts}   Passed: {m.TotalQuizPassed}", _labelStyle);
            GUILayout.Label($"  Last Score     : {m.LastQuizScore:P0}   Passed: {(m.LastQuizPassed ? "YES" : "NO")}", _labelStyle);
            GUILayout.Label($"  Last Time      : {m.LastQuizTime:F1}s", _labelStyle);
            GUILayout.Label($"  Avg Score      : {m.AverageQuizScore:P0}", _labelStyle);
        }
        else
        {
            GUILayout.Label("  No quiz data yet.", _dimStyle);
        }

        GUILayout.Space(4);
        GUILayout.Label($"  [H] to toggle HUD", _dimStyle);

        GUILayout.EndArea();
        GUI.color = prevColor;
    }

    // ── GUI Helpers ────────────────────────────────────────────────────────

    private void DrawBar(string label, float value, Color fillColour)
    {
        value = Mathf.Clamp01(value);
        Rect r = GUILayoutUtility.GetRect(panelWidth - 16, 16);

        // Track
        GUI.color = new Color(1f, 1f, 1f, 0.12f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        // Fill
        Rect fill = new Rect(r.x, r.y, r.width * value, r.height);
        GUI.color = new Color(fillColour.r, fillColour.g, fillColour.b, 0.75f);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);

        // Label + percentage overlaid
        GUI.color = Color.white;
        GUI.Label(r, $"  {label}  {value:P0}", _dimStyle);
    }

    private void HorizontalRule()
    {
        GUILayout.Space(2);
        Rect r = GUILayoutUtility.GetRect(panelWidth - 16, 1);
        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUILayout.Space(2);
    }

    private void BuildStyles()
    {
        if (_stylesReady) return;

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = Texture2D.whiteTexture }
        };

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = Color.white }
        };

        _dimStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
        };

        _stylesReady = true;
    }
}