// PuzzleUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;


public class PuzzleUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI questionText;
    public TextMeshProUGUI questionCounter;   // e.g. "Question 1 / 5"
    public Button[] answerButtons;            // 4 buttons
    public TextMeshProUGUI feedbackText;
    public Button closeButton;
    public GameObject summaryPanel;           // Shown at the end
    public TextMeshProUGUI summaryText;       // "You got 4/5 correct!"

    public static event Action OnPuzzleCompleted;               // fires every time puzzle is closed after finishing
    public static event Action<int, int> OnPuzzleFinished;      // fires with (correctCount, totalQuestions)

    private QuestionData[] _questions;
    private Action<bool> _onClose;
    private int _currentIndex;
    private int _correctCount;

    public void Setup(QuestionData[] questions, Action<bool> onClose)
    {
        if (questionText == null || answerButtons == null || feedbackText == null)
        {
            Debug.LogError("PuzzleUI: UI references not assigned!");
            return;
        }

        _questions = questions;
        _onClose = onClose;
        _currentIndex = 0;
        _correctCount = 0;

        summaryPanel.SetActive(false);
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => _onClose?.Invoke(false));

        LoadQuestion(_currentIndex);
    }

    void LoadQuestion(int index)
    {
        feedbackText.gameObject.SetActive(false);
        summaryPanel.SetActive(false);

        QuestionData q = _questions[index];
        questionText.text = q.question;
        questionCounter.text = $"Question {index + 1} / {_questions.Length}";

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int captured = i;
            answerButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = q.answers[i];
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() => OnAnswerSelected(captured));
            answerButtons[i].interactable = true;

            // Reset button color
            ColorBlock cb = answerButtons[i].colors;
            cb.normalColor = Color.white;
            answerButtons[i].colors = cb;
        }
    }

    void OnAnswerSelected(int index)
    {
        bool correct = index == _questions[_currentIndex].correctAnswerIndex;

        // Visual feedback on buttons
        ColorBlock cb = answerButtons[index].colors;
        cb.normalColor = correct ? Color.green : Color.red;
        answerButtons[index].colors = cb;

        // If wrong, also highlight correct answer
        if (!correct)
        {
            int correctIdx = _questions[_currentIndex].correctAnswerIndex;
            ColorBlock correctCb = answerButtons[correctIdx].colors;
            correctCb.normalColor = Color.green;
            answerButtons[correctIdx].colors = correctCb;
        }

        if (correct) _correctCount++;

        feedbackText.gameObject.SetActive(true);
        feedbackText.text = correct ? "Correct!" : "Wrong!";
        feedbackText.color = correct ? Color.green : Color.red;

        // Disable all buttons after answering
        foreach (var btn in answerButtons)
            btn.interactable = false;

        // Wait then advance
        StartCoroutine(NextQuestionDelay(1.5f));
    }

    void NextQuestion()
    {
        _currentIndex++;

        if (_currentIndex < _questions.Length)
        {
            LoadQuestion(_currentIndex);
        }
        else
        {
            ShowSummary();
        }
    }

    private IEnumerator NextQuestionDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay); // Ignores timeScale
        NextQuestion();
    }

    void ShowSummary()
    {
        questionText.gameObject.SetActive(false);
        questionCounter.gameObject.SetActive(false);
        feedbackText.gameObject.SetActive(false);
        foreach (var btn in answerButtons)
            btn.gameObject.SetActive(false);

        summaryPanel.SetActive(true);
        summaryText.text = $"You got {_correctCount} / {_questions.Length} correct!\n\n" +
                           (_correctCount == _questions.Length ? "Perfect score!" :
                            _correctCount >= 3 ? "Good job!" : "Keep trying!");

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() =>
        {
            // Restore hidden elements for next time
            questionText.gameObject.SetActive(true);
            questionCounter.gameObject.SetActive(true);
            foreach (var btn in answerButtons)
                btn.gameObject.SetActive(true);

            _onClose?.Invoke(_correctCount == _questions.Length);

            OnPuzzleCompleted?.Invoke();
            OnPuzzleFinished?.Invoke(_correctCount, _questions.Length);
        });
    }
}