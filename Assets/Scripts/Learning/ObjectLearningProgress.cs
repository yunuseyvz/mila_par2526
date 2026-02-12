using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LanguageTutor.Learning
{
    /// <summary>
    /// Manages vocabulary learning progress for detected objects.
    /// Uses spaced repetition logic (similar to Anki) to track object learning.
    /// </summary>
    public class ObjectLearningProgress : MonoBehaviour
    {
        [SerializeField] private string progressFileName = "vocabulary_progress.json";
        private string _progressFilePath;

        private Dictionary<string, ObjectWordData> _wordProgress = new Dictionary<string, ObjectWordData>();

        [System.Serializable]
        public class ObjectWordData
        {
            public string label;
            public int count = 0;  // Difficulty counter: higher = easier (seen more), lower/negative = harder (more mistakes)
            public string lastAsked = null;  // ISO 8601 format string instead of DateTime
            public int correctCount = 0;
            public int incorrectCount = 0;
        }

        private void Awake()
        {
            Debug.Log("[ObjectLearningProgress] Awake() called");
            _progressFilePath = Path.Combine(Application.persistentDataPath, progressFileName);
            Debug.Log($"[ObjectLearningProgress] Path set to: {_progressFilePath}");
            LoadProgress();  // Load existing data first
            Debug.Log($"[ObjectLearningProgress] Loaded {_wordProgress.Count} words");
        }

        /// <summary>
        /// Get all tracked vocabulary with their progress.
        /// </summary>
        public Dictionary<string, ObjectWordData> GetAllProgress()
        {
            return new Dictionary<string, ObjectWordData>(_wordProgress);
        }

        /// <summary>
        /// Get progress for a specific word.
        /// </summary>
        public ObjectWordData GetProgress(string label)
        {
            if (_wordProgress.TryGetValue(label.ToLower().Trim(), out var data))
            {
                return data;
            }
            return null;
        }

        /// <summary>
        /// Register a word as detected (initialize if missing).
        /// </summary>
        public void RegisterWord(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return;

            string key = label.ToLower().Trim();
            if (!_wordProgress.ContainsKey(key))
            {
                _wordProgress[key] = new ObjectWordData
                {
                    label = label.Trim(),
                    count = 0,
                    lastAsked = null,
                    correctCount = 0,
                    incorrectCount = 0
                };
                Debug.Log($"[ObjectLearningProgress] Registered word: {label}");
                SaveProgress();  // Save immediately when new word is registered
            }
        }

        /// <summary>
        /// Mark word as asked and increment count (correct answer).
        /// </summary>
        public void MarkCorrect(string label)
        {
            string key = label.ToLower().Trim();
            if (_wordProgress.TryGetValue(key, out var data))
            {
                data.count++;
                data.correctCount++;
                data.lastAsked = DateTime.Now.ToString("O");
                SaveProgress();
                Debug.Log($"[ObjectLearningProgress] CORRECT: '{label}' → count={data.count}, correct={data.correctCount}");
            }
        }

        /// <summary>
        /// Mark word as asked but user answered incorrectly.
        /// </summary>
        public void MarkIncorrect(string label)
        {
            string key = label.ToLower().Trim();
            if (_wordProgress.TryGetValue(key, out var data))
            {
                data.count--;
                data.incorrectCount++;
                data.lastAsked = DateTime.Now.ToString("O");
                SaveProgress();
                Debug.Log($"[ObjectLearningProgress] INCORRECT: '{label}' → count={data.count}, incorrect={data.incorrectCount}");
            }
        }

        /// <summary>
        /// Get next word to ask based on spaced repetition (lowest count first, then by last asked time).
        /// </summary>
        public string GetNextWordToAsk(List<string> availableWords)
        {
            if (availableWords == null || availableWords.Count == 0)
                return null;

            string nextWord = null;
            int lowestCount = int.MaxValue;
            string oldestLastAsked = null;

            foreach (var word in availableWords)
            {
                string key = word.ToLower().Trim();
                if (_wordProgress.TryGetValue(key, out var data))
                {
                    if (data.count < lowestCount ||
                        (data.count == lowestCount && (oldestLastAsked == null || string.Compare(data.lastAsked, oldestLastAsked) < 0)))
                    {
                        nextWord = word;
                        lowestCount = data.count;
                        oldestLastAsked = data.lastAsked;
                    }
                }
            }

            return nextWord ?? availableWords[0];
        }

        /// <summary>
        /// Save progress to JSON file.
        /// </summary>
        public void SaveProgress()
        {
            Debug.Log("[ObjectLearningProgress] SaveProgress() called");
            try
            {
                if (string.IsNullOrEmpty(_progressFilePath))
                {
                    Debug.LogError("[ObjectLearningProgress] FilePath is null or empty!");
                    return;
                }

                var dataList = new List<ObjectWordData>(_wordProgress.Values);
                Debug.Log($"[ObjectLearningProgress] Serializing {dataList.Count} words");

                string json = JsonUtility.ToJson(new ObjectWordDataList { data = dataList }, true);
                Debug.Log($"[ObjectLearningProgress] JSON created, length={json.Length}");

                File.WriteAllText(_progressFilePath, json);
                Debug.Log($"[ObjectLearningProgress] ✅ Progress saved to {_progressFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ObjectLearningProgress] ❌ Failed to save progress: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>
        /// Load progress from JSON file.
        /// </summary>
        private void LoadProgress()
        {
            try
            {
                if (!File.Exists(_progressFilePath))
                {
                    Debug.Log($"[ObjectLearningProgress] No progress file found at {_progressFilePath}");
                    return;
                }

                string json = File.ReadAllText(_progressFilePath);
                var dataList = JsonUtility.FromJson<ObjectWordDataList>(json);

                _wordProgress.Clear();
                if (dataList?.data != null)
                {
                    foreach (var item in dataList.data)
                    {
                        _wordProgress[item.label.ToLower().Trim()] = item;
                    }
                }

                Debug.Log($"[ObjectLearningProgress] Loaded {_wordProgress.Count} words from progress file");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ObjectLearningProgress] Failed to load progress: {e.Message}");
            }
        }

        [System.Serializable]
        private class ObjectWordDataList
        {
            public List<ObjectWordData> data = new List<ObjectWordData>();
        }
    }
}
