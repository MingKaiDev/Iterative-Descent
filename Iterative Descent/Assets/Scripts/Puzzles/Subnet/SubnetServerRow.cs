using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One server row inside the subnet puzzle table.
///
/// Layout (HorizontalLayoutGroup, fixed widths via LayoutElement):
///
///   [StatusDot]  [ServerLabel]  [HostsText]  [PrefixDisplay]  [LeftBtn] [/N] [RightBtn]
///
/// StatusDot  -- small square Image; grey=unset, green=correct, red=wrong
/// ServerLabel -- TMP, monospaced, left-aligned  (width ~200)
/// HostsText   -- TMP "Hosts: XXX", right-aligned (width ~120)
/// PrefixDisplay -- TMP "/XX", centre-aligned    (width ~80)
/// LeftBtn/RightBtn -- "<" / ">" buttons to cycle through CidrOptions
///
/// INSPECTOR WIRING (set in Inspector; all are children of this row GO):
///   statusDot, serverLabel, hostsText, prefixDisplay, leftButton, rightButton
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SubnetServerRow : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private Image            statusDot;
    [SerializeField] private TextMeshProUGUI  serverLabel;
    [SerializeField] private TextMeshProUGUI  hostsText;
    [SerializeField] private TextMeshProUGUI  prefixDisplay;
    [SerializeField] private Button           leftButton;
    [SerializeField] private Button           rightButton;

    // ── Colours (CPT palette) ─────────────────────────────────────────────────

    private static readonly Color ColNeutral = new Color(0.44f, 0.55f, 0.62f); // steel blue-grey
    private static readonly Color ColCorrect = new Color(0.18f, 0.80f, 0.44f); // CPT green
    private static readonly Color ColWrong   = new Color(0.90f, 0.25f, 0.25f); // CPT red

    // ── Runtime ───────────────────────────────────────────────────────────────

    private int[] _options;
    private int   _selectedIndex;

    public int SelectedPrefix => _options != null ? _options[_selectedIndex] : -1;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (leftButton  != null) leftButton.onClick.AddListener(OnLeft);
        if (rightButton != null) rightButton.onClick.AddListener(OnRight);
    }

    private void OnDisable()
    {
        if (leftButton  != null) leftButton.onClick.RemoveListener(OnLeft);
        if (rightButton != null) rightButton.onClick.RemoveListener(OnRight);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the row. Called from SubnetPuzzleUI.InitPuzzle().
    /// Starts selector at middle of the options range (not at the answer).
    /// </summary>
    public void Setup(string label, int hostsNeeded, int[] cidrOptions)
    {
        _options       = cidrOptions;
        _selectedIndex = cidrOptions.Length / 2; // start mid-range, not at answer

        if (serverLabel  != null) serverLabel.text  = label;
        if (hostsText    != null) hostsText.text     = $"Hosts req: {hostsNeeded}";

        SetDot(ColNeutral);
        RefreshPrefix();
    }

    /// <summary>
    /// Highlights the row green (correct) or red (wrong).
    /// </summary>
    public void SetHighlight(bool correct) => SetDot(correct ? ColCorrect : ColWrong);

    /// <summary>
    /// Resets highlight to neutral (called before re-check on next submit).
    /// </summary>
    public void ResetHighlight() => SetDot(ColNeutral);

    // ── Cycling ───────────────────────────────────────────────────────────────

    private void OnLeft()
    {
        if (_options == null) return;
        _selectedIndex = (_selectedIndex - 1 + _options.Length) % _options.Length;
        ResetHighlight();
        RefreshPrefix();
    }

    private void OnRight()
    {
        if (_options == null) return;
        _selectedIndex = (_selectedIndex + 1) % _options.Length;
        ResetHighlight();
        RefreshPrefix();
    }

    private void RefreshPrefix()
    {
        if (prefixDisplay != null && _options != null)
            prefixDisplay.text = $"/{_options[_selectedIndex]}";
    }

    private void SetDot(Color c)
    {
        if (statusDot != null)
            statusDot.color = c;
    }
}
