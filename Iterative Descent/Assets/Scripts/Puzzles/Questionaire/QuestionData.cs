// QuestionData.cs
using System;

[Serializable]
public class QuestionData
{
    public string question;
    public string[] answers = new string[4];
    public int correctAnswerIndex;

    /// <summary>
    /// One or two sentence explanation of why the correct answer is correct.
    /// Shown to the player after they answer (right or wrong) so the quiz teaches
    /// the concept instead of only testing it. Leave empty to fall back to the
    /// plain "Correct!"/"Wrong!" feedback with no explanation line.
    /// </summary>
    public string explanation;

    /// <summary>
    /// Difficulty tier as authored in quiz_bank.json (1 = easiest, 3 = hardest).
    /// Was present in the JSON but silently dropped by JsonUtility because this
    /// field didn't exist yet -- added so QuestionSelector can filter by it
    /// (e.g. keeping a brand-new player's first session on difficulty 1 only).
    /// Defaults to 0 for any question that doesn't set it, which sorts as
    /// "easier than difficulty 1" wherever this is compared numerically.
    /// </summary>
    public int difficulty;

    /// <summary>
    /// BKT concept this question tests.
    /// Set to one of the constant strings on BayesianKnowledgeTracker, e.g.:
    ///   BayesianKnowledgeTracker.AlgorithmComplexity
    ///   BayesianKnowledgeTracker.OperatingSystems
    ///   BayesianKnowledgeTracker.LinkedLists
    ///   BayesianKnowledgeTracker.CpuScheduling
    /// Leave empty to skip BKT tracking for this question.
    /// </summary>
    public string conceptTag;
}