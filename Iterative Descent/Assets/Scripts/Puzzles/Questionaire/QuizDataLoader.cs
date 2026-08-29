// QuizDataLoader.cs
// Loads QuestionData arrays from a JSON file in StreamingAssets.
//
// Uses UnityWebRequest instead of System.IO.File. StreamingAssets is not a
// real local file on WebGL (served over HTTP) or Android (packed inside the
// APK/AAB) -- System.IO.File.Exists()/ReadAllText() silently fail on those
// platforms even though they work fine in the Editor and in a Windows/Mac
// Standalone build. That mismatch is why this loader worked in Play Mode but
// returned null in a WebGL build, silently falling back to whatever (usually
// empty) Inspector "questions" array was on the prop.
//
// Usage (from a MonoBehaviour):
//   yield return QuizDataLoader.LoadCoroutine("quiz_bank.json", result => questions = result);
//
// The JSON must be an object with a "questions" array, e.g.:
// {
//   "questions": [
//     { "question": "...", "answers": ["A","B","C","D"], "correctAnswerIndex": 0 },
//     ...
//   ]
// }

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class QuizDataLoader
{
    [Serializable]
    private class QuizDataWrapper
    {
        public QuestionData[] questions;
    }

    /// <summary>
    /// Loads questions from a JSON file in StreamingAssets and invokes
    /// onComplete with the parsed array, or null if the request or the parse
    /// failed. Same "null means fall back to Inspector questions" contract
    /// as the old synchronous Load() -- callers don't need to change their
    /// fallback logic, only how they invoke this (as a coroutine).
    /// </summary>
    /// <param name="fileName">File name relative to StreamingAssets (e.g. "quiz_bank.json")</param>
    /// <param name="onComplete">Called exactly once with the result.</param>
    public static IEnumerator LoadCoroutine(string fileName, Action<QuestionData[]> onComplete)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        using (UnityWebRequest request = UnityWebRequest.Get(path))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool failed = request.result == UnityWebRequest.Result.ConnectionError
                       || request.result == UnityWebRequest.Result.ProtocolError
                       || request.result == UnityWebRequest.Result.DataProcessingError;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif

            if (failed)
            {
                Debug.LogError($"[QuizDataLoader] Failed to load '{path}': {request.error}");
                onComplete(null);
                yield break;
            }

            string json = request.downloadHandler.text;
            QuizDataWrapper wrapper = JsonUtility.FromJson<QuizDataWrapper>(json);

            if (wrapper == null || wrapper.questions == null || wrapper.questions.Length == 0)
            {
                Debug.LogError($"[QuizDataLoader] Failed to parse questions from: {path}");
                onComplete(null);
                yield break;
            }

            Debug.Log($"[QuizDataLoader] Loaded {wrapper.questions.Length} questions from {fileName}");
            onComplete(wrapper.questions);
        }
    }
}
