// PuzzleUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PuzzleUI : MonoBehaviour
{
    public TextMeshProUGUI questionText;
    public Button[] answerButtons;        // 4 buttons in your panel
    public TextMeshProUGUI feedbackText;
    public Button closeButton;

    private Action<bool> _onClose;
    private int _correctIndex;

    public void Setup(string question, string[] answers, int correctIndex, Action<bool> onClose)
    {
        _onClose = onClose;
        _correctIndex = correctIndex;
        feedbackText.gameObject.SetActive(false);

        questionText.text = question;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int capturedIndex = i; // Capture for lambda closure
            answerButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = answers[i];
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() => OnAnswerSelected(capturedIndex));
            answerButtons[i].interactable = true;
        }

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => _onClose?.Invoke(false));
    }

    void OnAnswerSelected(int index)
    {
        bool correct = index == _correctIndex;
        feedbackText.gameObject.SetActive(true);

        if (correct)
        {
            feedbackText.text = "✓ Correct!";
            feedbackText.color = Color.green;
            // Auto-close after short delay
            Invoke(nameof(CloseCorrect), 1.5f);
        }
        else
        {
            feedbackText.text = "✗ Try again!";
            feedbackText.color = Color.red;
        }

        // Disable all buttons after answer
        foreach (var btn in answerButtons)
            btn.interactable = false;
    }

    void CloseCorrect() => _onClose?.Invoke(true);
}