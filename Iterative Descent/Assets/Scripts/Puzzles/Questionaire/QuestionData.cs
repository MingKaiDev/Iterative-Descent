// QuestionData.cs
using System;

[Serializable]
public class QuestionData
{
    public string question;
    public string[] answers = new string[4];
    public int correctAnswerIndex;
} 