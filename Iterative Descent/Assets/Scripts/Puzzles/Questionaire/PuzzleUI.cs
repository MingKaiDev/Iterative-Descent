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

    // ── Static event ─────────────────────────────────────────────
    public static event Action<int, int> OnPuzzleFinished;

    // ── Runtime ──────────────────────────────────────────────────
    private QuestionData[] _questions;
    private Action<bool> _onClose;
    private int _currentIndex;
    private int _correctCount;
    private bool _answered;

    // ────────────────────────────────────────────────────────────

    void Awake()
    {
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

            // Ensure text is white
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = textColour;
        }
    }

    public void Setup(QuestionData[] questions, Action<bool> onClose)
    {
        PlayerMetricsTracker.Instance?.NotifyQuizStarted();
        _questions = questions;
        _onClose = onClose;
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

        SetButtonImageColour(answerButtons[chosen], correct ? correctColour : wrongColour);
        if (!correct)
            SetButtonImageColour(
                answerButtons[_questions[_currentIndex].correctAnswerIndex], correctColour);

        feedbackText.text = correct ? "Correct!" : "Wrong!";
        if (correct) _correctCount++;

        foreach (var btn in answerButtons) btn.interactable = false;
        StartCoroutine(AdvanceAfterDelay(1.4f));
    }

    IEnumerator AdvanceAfterDelay(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        _currentIndex++;
        ShowQuestion(_currentIndex);
    }

    void ShowCompletion()
    {
        bool allCorrect = _correctCount == _questions.Length;
        questionText.text = allCorrect
            ? "All questions answered correctly!"
            : $"Finished!  {_correctCount} / {_questions.Length} correct.";
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