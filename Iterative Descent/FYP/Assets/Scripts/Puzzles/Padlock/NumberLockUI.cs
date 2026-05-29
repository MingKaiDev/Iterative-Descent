using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 4-digit combination lock UI.
/// A / D  -> shift selected digit left / right
/// W / S  -> increment / decrement selected digit
/// Enter  -> submit
/// Escape -> close (via PadlockProp)
/// </summary>
public class NumberLockUI : MonoBehaviour
{
    [Header("Digit Displays")]
    [SerializeField] private TextMeshProUGUI[] digitTexts;       // 4 elements, index 0 = leftmost
    [SerializeField] private Image[]           digitHighlights;  // 4 background images, tinted on selection

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI feedbackText;       // "ACCESS DENIED" / "UNLOCKED"

    [Header("Colours")]
    [SerializeField] private Color normalColour    = new Color(0.25f, 0.22f, 0.18f, 1f);
    [SerializeField] private Color selectedColour  = new Color(0.80f, 0.65f, 0.20f, 1f);
    [SerializeField] private Color deniedColour    = new Color(0.80f, 0.10f, 0.10f, 1f);
    [SerializeField] private Color unlockedColour  = new Color(0.20f, 0.70f, 0.25f, 1f);

    // State
    private int[] digits       = new int[4];
    private int   selectedIndex = 0;
    private bool  isOpen       = false;

    // Callback so PadlockProp knows when to close
    public event Action OnRequestClose;

    // Exposes the entered combination as a string (e.g. "0000")
    public string EnteredCode => string.Concat(digits);

    // -----------------------------------------------------------------------

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isOpen) return;

        // Shift selection
        if (Input.GetKeyDown(KeyCode.A))
            ShiftSelection(-1);
        if (Input.GetKeyDown(KeyCode.D))
            ShiftSelection(1);

        // Spin digit
        if (Input.GetKeyDown(KeyCode.W))
            SpinDigit(1);
        if (Input.GetKeyDown(KeyCode.S))
            SpinDigit(-1);

        // Submit
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Submit();

        // Close
        if (Input.GetKeyDown(KeyCode.Escape))
            OnRequestClose?.Invoke();
    }

    // -----------------------------------------------------------------------
    // Public API

    /// <summary>Called by PadlockProp to open the lock UI.</summary>
    /// <param name="correctCode">The code that unlocks (null = always deny).</param>
    public void Open(string correctCode = null)
    {
        _correctCode   = correctCode;
        selectedIndex  = 0;
        for (int i = 0; i < 4; i++) digits[i] = 0;

        ClearFeedback();
        RefreshAll();

        gameObject.SetActive(true);
        isOpen = true;
    }

    public void Close()
    {
        isOpen = false;
        gameObject.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Internal

    private string _correctCode;

    private void ShiftSelection(int dir)
    {
        selectedIndex = Mathf.Clamp(selectedIndex + dir, 0, 3);
        RefreshHighlights();
    }

    private void SpinDigit(int dir)
    {
        digits[selectedIndex] = (digits[selectedIndex] + dir + 10) % 10;
        RefreshDigit(selectedIndex);
    }

    private void Submit()
    {
        if (!string.IsNullOrEmpty(_correctCode) && EnteredCode == _correctCode)
        {
            ShowFeedback("UNLOCKED", unlockedColour);
            // PadlockProp listens to this event to trigger unlock behaviour
            OnUnlocked?.Invoke();
        }
        else
        {
            ShowFeedback("ACCESS DENIED", deniedColour);
        }
    }

    // Fired when the correct code is entered
    public event Action OnUnlocked;

    private void RefreshAll()
    {
        for (int i = 0; i < 4; i++) RefreshDigit(i);
        RefreshHighlights();
    }

    private void RefreshDigit(int i)
    {
        if (digitTexts != null && i < digitTexts.Length && digitTexts[i] != null)
            digitTexts[i].text = digits[i].ToString();
    }

    private void RefreshHighlights()
    {
        if (digitHighlights == null) return;
        for (int i = 0; i < digitHighlights.Length; i++)
        {
            if (digitHighlights[i] == null) continue;
            digitHighlights[i].color = (i == selectedIndex) ? selectedColour : normalColour;
        }
    }

    private void ShowFeedback(string msg, Color col)
    {
        if (feedbackText == null) return;
        feedbackText.text  = msg;
        feedbackText.color = col;
        feedbackText.gameObject.SetActive(true);
    }

    private void ClearFeedback()
    {
        if (feedbackText == null) return;
        feedbackText.text = "";
        feedbackText.gameObject.SetActive(false);
    }
}
