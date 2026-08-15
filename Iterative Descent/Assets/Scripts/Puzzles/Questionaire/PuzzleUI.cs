using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PuzzleUI : MonoBehaviour
{
    [Header("Text Elements")]
    public TMP_Text questionText;
    public TMP_Text progressText;
    public TMP_Text feedbackText;

    [Header("Answer Buttons (exactly 4)")]
    public Button[] answerButtons = new Button[4];

    [Header("Close Button")]
    public Button closeButton;

    [Header("Colours")]
    public Color defaultColour = new Color(0.15f, 0.15f, 0.15f, 1f);
    public Color correctColour = new Color(0.1f, 0.6f, 0.1f, 1f);
    public Color wrongColour = new Color(0.7f, 0.1f, 0.1f, 1f);
    public Color textColour = Color.white;

    [Header("Button Text Sizing")]
    [Tooltip("Maximum font size for answer button labels")]
    public float buttonFontSizeMax = 18f;
    [Tooltip("Minimum font size -- auto-shrinks to fit long strings")]
    public float buttonFontSizeMin = 10f;

    // ── Static events ─────────────────────────────────────────────

    /// <summary>
    /// Fires once per question the moment the player selects an answer.
    /// Used by BayesianKnowledgeTracker to update P(knows) per concept.
    /// Parameters: conceptTag (string), wasCorrect (bool).
    /// Empty conceptTag means the question has no BKT concept assigned -- skip it.
    /// </summary>
    public static event Action<string, bool> OnQuestionAnswered;

    /// <summary>Fires once when all questions are done. Parameters: correct count, total count.</summary>
    public static event Action<int, int> OnPuzzleFinished;

    // ── Runtime ──────────────────────────────────────────────────
    private QuestionData[] _questions;
    private Action<bool> _onClose;
    private int _currentIndex;
    private int _correctCount;
    private bool _answered;

    // Optional -- if set via Setup(), and the player passes this quiz session
    // (see the threshold in ShowCompletion), this literal string is shown on
    // the completion screen instead of the generic pass/fail text. Used by
    // the Prop_Desk_Folder quiz to reveal the PC password as the reward.
    // Left null for every other quiz that reuses this same PuzzleUI panel.
    private string _passwordToReveal;

    // ────────────────────────────────────────────────────────────

    void Awake()
    {
        // Defensive TMP config for the two free-text fields. This does not
        // replace giving them a properly sized RectTransform in the Editor,
        // but it guarantees text can never blow past the notebook panel again
        // even if a future string (or the current explanation text) turns out
        // longer than whatever box size is configured there right now.
        ConfigureWrappingText(questionText);
        ConfigureWrappingText(feedbackText);

        // Force button color blocks to pure white so our Image colour
        // is never tinted or multiplied by Unity's button state colours
        foreach (var btn in answerButtons)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            cb.colorMultiplier = 1f;
            btn.colors = cb;

            // Configure label: colour, auto-size, word wrap
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.color = textColour;
                label.enableWordWrapping = true;
                label.enableAutoSizing = true;
                label.fontSizeMin = buttonFontSizeMin;
                label.fontSizeMax = buttonFontSizeMax;
                label.overflowMode = TMPro.TextOverflowModes.Overflow;
                label.alignment = TMPro.TextAlignmentOptions.Center;
            }
        }
    }

    public void Setup(QuestionData[] questions, Action<bool> onClose, string passwordToReveal = null)
    {
        PlayerMetricsTracker.Instance?.NotifyQuizStarted();
        _questions = questions;
        _onClose = onClose;
        _passwordToReveal = passwordToReveal;
        _currentIndex = 0;
        _correctCount = 0;
        _answered = false;

        foreach (var btn in answerButtons)
        {
            btn.gameObject.SetActive(true);
            SetButtonImageColour(btn, defaultColour);
        }

        closeButton.gameObject.SetActive(false);
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(OnCloseClicked);

        feedbackText.text = "";
        ShowQuestion(0);
    }

    void ShowQuestion(int index)
    {
        if (index >= _questions.Length) { ShowCompletion(); return; }

        _answered = false;
        QuestionData q = _questions[index];
        questionText.text = q.question;
        progressText.text = $"Question {index + 1} / {_questions.Length}";
        feedbackText.text = "";

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int captured = i;
            answerButtons[i].gameObject.SetActive(true);
            answerButtons[i].interactable = true;
            answerButtons[i].GetComponentInChildren<TMP_Text>().text = q.answers[i];
            SetButtonImageColour(answerButtons[i], defaultColour);
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() => OnAnswerSelected(captured));
        }
    }

    void OnAnswerSelected(int chosen)
    {
        if (_answered) return;
        _answered = true;

        bool correct = chosen == _questions[_currentIndex].correctAnswerIndex;

        // Notify BKT immediately — before the visual delay
        string tag = _questions[_currentIndex].conceptTag;
        if (!string.IsNullOrEmpty(tag))
            OnQuestionAnswered?.Invoke(tag, correct);

        SetButtonImageColour(answerButtons[chosen], correct ? correctColour : wrongColour);
        if (!correct)
            SetButtonImageColour(
                answerButtons[_questions[_currentIndex].correctAnswerIndex], correctColour);

        // feedbackText only ever holds this short headline now -- the same string
        // it always held before explanations existed, so it stays within whatever
        // small box it was already sized for.
        feedbackText.text = correct ? "Correct!" : "Wrong!";
        if (correct) _correctCount++;

        foreach (var btn in answerButtons) btn.interactable = false;
        StartCoroutine(RevealExplanationThenAdvance(_questions[_currentIndex].explanation));
    }

    // Timing constants for the reveal-then-advance sequence.
    const float HeadlineHoldSeconds = 0.9f;      // time spent just showing "Correct!/Wrong!"
    const float BaseDelaySeconds = 1.4f;         // total pace when there's no explanation (matches old behaviour)
    const float MinExplanationReadSeconds = 2.0f;
    const float MaxExplanationReadSeconds = 5.0f;
    const float CharsPerSecondReadingSpeed = 35f; // faster than average silent reading --
                                                   // this is reinforcement text after the player
                                                   // already read the question, not a first read

    // Explanation is shown in questionText's area (not feedbackText) because that
    // field already has a properly sized, word-wrapping container -- proven by the
    // fact that two-line questions already render correctly there. feedbackText was
    // never sized for a full sentence and overflowed the notebook panel when explanations
    // were placed there.
    IEnumerator RevealExplanationThenAdvance(string explanation)
    {
        yield return new WaitForSecondsRealtime(HeadlineHoldSeconds);

        if (!string.IsNullOrEmpty(explanation))
        {
            questionText.text = explanation;
            float readSeconds = Mathf.Clamp(explanation.Length / CharsPerSecondReadingSpeed, MinExplanationReadSeconds, MaxExplanationReadSeconds);
            yield return new WaitForSecondsRealtime(readSeconds);
        }
        else
        {
            float remaining = BaseDelaySeconds - HeadlineHoldSeconds;
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
        }

        _currentIndex++;
        ShowQuestion(_currentIndex);
    }

    // Enables word wrap + auto-shrink-to-fit + ellipsis overflow as a last resort,
    // so this field can never again render text past the bounds of its RectTransform,
    // no matter how long a future string turns out to be. Does not replace giving it
    // a properly sized box in the Editor -- this is a safety net, not a layout fix.
    void ConfigureWrappingText(TMP_Text text)
    {
        if (text == null) return;
        text.enableWordWrapping = true;
        text.enableAutoSizing = true;
        text.fontSizeMin = buttonFontSizeMin;
        text.fontSizeMax = Mathf.Max(text.fontSize, buttonFontSizeMax);
        text.overflowMode = TMPro.TextOverflowModes.Ellipsis;
    }

    void ShowCompletion()
    {
        bool allCorrect = _correctCount == _questions.Length;

        // 60% pass threshold -- keep this formula in sync with
        // PasswordEventHandler.HandlePuzzleFinished(), which uses the same
        // check to decide whether to unlock the password auto-type on the
        // PC login screen. This local copy only controls what the
        // completion screen displays.
        int required = Mathf.CeilToInt(_questions.Length * 0.6f);
        bool passed = _correctCount >= required;

        if (passed && !string.IsNullOrEmpty(_passwordToReveal))
        {
            questionText.text = $"Computer Password is {_passwordToReveal}";
        }
        else
        {
            questionText.text = allCorrect
                ? "All questions answered correctly!"
                : $"Finished!  {_correctCount} / {_questions.Length} correct.";
        }
        progressText.text = "";
        feedbackText.text = "";

        foreach (var btn in answerButtons) btn.gameObject.SetActive(false);
        closeButton.gameObject.SetActive(true);

        OnPuzzleFinished?.Invoke(_correctCount, _questions.Length);
    }

    void OnCloseClicked()
    {
        foreach (var btn in answerButtons) btn.gameObject.SetActive(true);
        _onClose?.Invoke(_correctCount == _questions.Length);
    }

    // ── Colour helper — sets the button's Image directly ─────────
    void SetButtonImageColour(Button btn, Color c)
    {
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = c;
    }
}