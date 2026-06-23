// QuizDataLoader.cs
// Loads QuestionData arrays from a JSON file in StreamingAssets.
// Usage:
//   QuestionData[] questions = QuizDataLoader.Load("quiz_data.json");
//
// The JSON must be an object with a "questions" array, e.g.:
// {
//   "questions": [
//     { "question": "...", "answers": ["A","B","C","D"], "correctAnswerIndex": 0 },
//     ...
//   ]
// }

using System;
using System.IO;
using UnityEngine;

public static class QuizDataLoader
{
    [Serializable]
    private class QuizDataWrapper
    {
        public QuestionData[] questions;
    }

    /// <summary>
    /// Loads questions from a JSON file in StreamingAssets.
    /// Returns null and logs an error if the file is missing or malformed.
    /// </summary>
    /// <param name="fileName">File name relative to StreamingAssets (e.g. "quiz_data.json")</param>
    public static QuestionData[] Load(string fileName = "quiz_bank.json")
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[QuizDataLoader] File not found: {path}");
            return null;
        }

        string json = File.ReadAllText(path);

        QuizDataWrapper wrapper = JsonUtility.FromJson<QuizDataWrapper>(json);

        if (wrapper == null || wrapper.questions == null || wrapper.questions.Length == 0)
        {
            Debug.LogError($"[QuizDataLoader] Failed to parse questions from: {path}");
            return null;
        }

        Debug.Log($"[QuizDataLoader] Loaded {wrapper.questions.Length} questions from {fileName}");
        return wrapper.questions;
    }
}
