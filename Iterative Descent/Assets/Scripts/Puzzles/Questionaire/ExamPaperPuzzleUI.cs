// ExamPaperPuzzleUI.cs
// Exam-paper styled MCQ quiz.
// Reuses QuestionData, OnQuestionAnswered, OnPuzzleFinished and PlayerMetricsTracker
// so BKT, DDA and PuzzleEventHandler all work without changes.
//
// Canvas hierarchy required:
//   ExamPaperPanel (this script)
//     PaperSheet (RectTransform, pivot 0.5,1 -- PageFlipAnimator lives here)
//       SpiralStrip (Image -- exam_spiral.png)
//       PaperBg     (Image -- exam_paper_bg.png)
//       Header
//         TitleText       (TMP)
//         InstitutionText (TMP)
//       MetaRow
//         NameText   (TMP) -- default "Aaron"
//         ScoreText  (TMP)
//         DateText   (TMP)
//         TimeText   (TMP)
//       QuestionLabel (TMP)   -- "Question X of Y"
//       QuestionText  (TMP)
//       OptionsContainer (VerticalLayoutGroup)
//         OptionRow_A .. OptionRow_D
//           BubbleButton (Button + Image -- exam_option_bubble.png)
//             BubbleLabel (TMP) "A"
//           OptionLabel (TMP)
//       AnswerBoxPanel (Image -- exam_answer_box.png)
//         AnswerFillText (TMP)
//         VerdictImage   (Image -- exam_tick.png / exam_cross.png, initially inactive)
//     NextButton (Button)
//     CompletionPanel (initially inactive)
//       CompletionText (TMP)
//       CloseButton (Button)

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExamPaperPuzzleUI : MonoBehaviour
{
    // Singleton set in Awake() -- same cross-scene-safe pattern as every other
    // puzzle UI in this project (see BstPuzzleUI.Instance). Lets ExamPaperProp
    // resolve this shared Canvas panel at runtime instead of relying only on
    // a serialized Inspector reference, which goes stale across a Level 1 ->
    // Level 2 -> Level 1 scene reload.
    public static ExamPaperPuzzleUI Instance { get; private set; }

    // ── Static events (identical contract to PuzzleUI) ───────────────────────

    /// <summary>Fires per question: conceptTag, wasCorrect.</summary>
    public static event Action<string, bool> OnQuestionAnswered;

    /// <summary>Fires when all questions are done: correctCount, totalCount.</summary>
    public static event Action<int, int> OnPuzzleFinished;

    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Flip")]
    [SerializeField] private PageFlipAnimator flipAnimator;

    [Header("Header Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text institutionText;

    [Header("Meta Fields")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text timeText;

    [Header("Question Area")]
    [SerializeField] private TMP_Text questionLabel;   // "Question X of Y"
    [SerializeField] private TMP_Text questionText;

    [Header("Option Rows (exactly 4, order A B C D)")]
    [SerializeField] private Button[]   optionButtons = new Button[4];
    [SerializeField] private TMP_Text[] optionLabels  = new TMP_Text[4];
    [SerializeField] private Image[]    optionBubbles = new Image[4];  // the circle image

    [Header("Answer Box")]
    [SerializeField] private TMP_Text answerFillText;
    [SerializeField] private Image    verdictImage;
    [SerializeField] private Sprite   tickSprite;
    [SerializeField] private Sprite   crossSprite;

    [Header("Navigation")]
    [SerializeField] private Button nextButton;

    [Header("Completion")]
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private TMP_Text   completionText;
    [SerializeField] private Button     closeButton;

    [Header("Colours")]
    [SerializeField] private Color bubbleDefaultColour  = new Color(0.96f, 0.94f, 0.89f, 1f);
    [SerializeField] private Color bubbleCorrectColour  = new Color(0.80f, 0.94f, 0.82f, 1f);
    [SerializeField] private Color bubbleWrongColour    = new Color(0.96f, 0.82f, 0.82f, 1f);
    [SerializeField] private Color bubbleSelectedColour = new Color(0.85f, 0.85f, 0.75f, 1f);

    [Header("Meta Defaults")]
    [SerializeField] private string defaultName = "Aaron";

    // ── Runtime ──────────────────────────────────────────────────────────────

    private QuestionData[] _questions;
    private Action<bool>   _onClose;
    private int            _currentIndex;
    private int            _correctCount;
    private bool           _answered;
    private int            _chosenIndex = -1;

    // ── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;

        // Safe defaults -- keep buttons non-interactive until Setup() is called
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextClicked);
            nextButton.interactable = false;
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        if (completionPanel != null) completionPanel.SetActive(false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Call this instead of PuzzleUI.Setup(). Same signature.
    /// NotifyQuizStarted() is called here -- do NOT call it in the prop too.
    /// </summary>
    public void Setup(QuestionData[] questions, Action<bool> onClose)
    {
        PlayerMetricsTracker.Instance?.NotifyQuizStarted();

        _questions    = questions;
        _onClose      = onClose;
        _currentIndex = 0;
        _correctCount = 0;
        _answered     = false;
        _chosenIndex  = -1;

        // Fill meta fields
        if (nameText        != null) nameText.text        = $"Name:  {defaultName}";
        if (institutionText != null) institutionText.text = "Nanyang Technological University";
        if (titleText       != null) titleText.text       = "Quiz";
        if (dateText        != null) dateText.text        = $"Date:  {System.DateTime.Now:dd/MM/yyyy}";

        if (completionPanel != null) completionPanel.SetActive(false);

        ShowQuestion(0);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void ShowQuestion(int index)
    {
        if (index >= _questions.Length)
        {
            ShowCompletion();
            return;
        }

        _answered    = false;
        _chosenIndex = -1;

        QuestionData q = _questions[index];

        if (questionLabel != null)
            questionLabel.text = $"Question {index + 1} of {_questions.Length}";

        if (questionText != null)
            questionText.text = q.question;

        // Score line: show running tally
        if (scoreText != null)
            scoreText.text = $"Score:  {_correctCount} / {_questions.Length}";

        // Reset options
        string[] prefixes = { "A", "B", "C", "D" };
        for (int i = 0; i < optionButtons.Length; i++)
        {
            int captured = i;

            if (optionLabels[i]  != null) optionLabels[i].text = q.answers[i];
            if (optionBubbles[i] != null) optionBubbles[i].color = bubbleDefaultColour;

            optionButtons[i].interactable = true;
            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => OnOptionSelected(captured));
        }

        // Reset answer box
        if (answerFillText != null) answerFillText.text = "";
        if (verdictImage   != null) verdictImage.gameObject.SetActive(false);

        // Next button locked until an answer is chosen
        if (nextButton != null) nextButton.interactable = false;
    }

    private void OnOptionSelected(int chosen)
    {
        if (_answered) return;
        _answered    = true;
        _chosenIndex = chosen;

        QuestionData q    = _questions[_currentIndex];
        bool correct      = chosen == q.correctAnswerIndex;
        string chosenText = q.answers[chosen];

        // BKT notification
        if (!string.IsNullOrEmpty(q.conceptTag))
            OnQuestionAnswered?.Invoke(q.conceptTag, correct);

        // Bubble colours
        for (int i = 0; i < optionBubbles.Length; i++)
        {
            if (optionBubbles[i] == null) continue;

            if (i == q.correctAnswerIndex)
                optionBubbles[i].color = bubbleCorrectColour;
            else if (i == chosen && !correct)
                optionBubbles[i].color = bubbleWrongColour;
            else
                optionBubbles[i].color = bubbleDefaultColour;
        }

        // Fill answer box
        string prefix = GetPrefix(chosen);
        if (answerFillText != null)
            answerFillText.text = $"{prefix}   {chosenText}";

        // Verdict mark
        if (verdictImage != null)
        {
            verdictImage.sprite = correct ? tickSprite : crossSprite;
            verdictImage.gameObject.SetActive(true);
        }

        if (correct) _correctCount++;

        // Disable all option buttons
        foreach (var btn in optionButtons) btn.interactable = false;

        // Enable NEXT
        if (nextButton != null) nextButton.interactable = true;
    }

    private void OnNextClicked()
    {
        if (flipAnimator != null && !flipAnimator.IsFlipping)
        {
            _currentIndex++;
            flipAnimator.Flip(OnFlipMidpoint);
        }
        else if (flipAnimator == null)
        {
            // Fallback: no flip animation, advance immediately
            _currentIndex++;
            ShowQuestion(_currentIndex);
        }
    }

    // Called by PageFlipAnimator at the midpoint (page is edge-on / invisible)
    private void OnFlipMidpoint()
    {
        ShowQuestion(_currentIndex);
    }

    private void ShowCompletion()
    {
        OnPuzzleFinished?.Invoke(_correctCount, _questions.Length);

        if (completionPanel != null)
        {
            completionPanel.SetActive(true);
            if (completionText != null)
                completionText.text = $"Completed!  {_correctCount} / {_questions.Length} correct.";
        }

        if (nextButton != null) nextButton.gameObject.SetActive(false);

        // Hide question content
        if (questionText  != null) questionText.text  = "";
        if (questionLabel != null) questionLabel.text = "";
        foreach (var btn in optionButtons) btn.gameObject.SetActive(false);
    }

    private void OnCloseClicked()
    {
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        foreach (var btn in optionButtons) btn.gameObject.SetActive(true);

        _onClose?.Invoke(_correctCount == _questions.Length);
    }

    private static string GetPrefix(int index)
    {
        switch (index)
        {
            case 0: return "A";
            case 1: return "B";
            case 2: return "C";
            case 3: return "D";
            default: return "?";
        }
    }
}
