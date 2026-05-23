// QuestionData.cs
using System;

[Serializable]
public class QuestionData
{
    public string question;
    public string[] answers = new string[4];
    public int correctAnswerIndex;

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