using System.Collections.Generic;
using UnityEngine;
using LanguageTutor.Learning;

namespace LanguageTutor.Detection
{
    /// <summary>
    /// Simple test component to verify ObjectLearningProgress JSON persistence.
    /// Registers detected objects and saves/loads progress.
    /// </summary>
    public class ObjectLearningProgressDebug : MonoBehaviour
    {
        [SerializeField] private ObjectDetectionListRecorder recorder;
        private ObjectLearningProgress _learningProgress;

        private void Start()
        {
            if (recorder == null)
                recorder = FindObjectOfType<ObjectDetectionListRecorder>();

            _learningProgress = GetComponent<ObjectLearningProgress>();
            if (_learningProgress == null)
                _learningProgress = FindObjectOfType<ObjectLearningProgress>();

            if (_learningProgress == null)
            {
                Debug.LogError("[ObjectLearningProgressDebug] ObjectLearningProgress not found!");
                return;
            }

            Debug.Log("[ObjectLearningProgressDebug] Started - Monitoring object detection");
        }

        private void Update()
        {
            if (recorder?.Registry == null || _learningProgress == null)
                return;

            // Register all detected objects
            foreach (var entry in recorder.Registry.Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Label))
                {
                    _learningProgress.RegisterWord(entry.Label);
                }
            }

            // Log progress every 5 seconds
            if (Time.frameCount % 300 == 0)  // ~5 seconds at 60 FPS
            {
                LogProgress();
            }
        }

        private void LogProgress()
        {
            if (_learningProgress == null)
                return;

            var allProgress = _learningProgress.GetAllProgress();
            if (allProgress.Count == 0)
            {
                Debug.Log("[ObjectLearningProgressDebug] No words tracked yet");
                return;
            }

            Debug.Log($"[ObjectLearningProgressDebug] === Vocabulary Progress ({allProgress.Count} words) ===");
            foreach (var kvp in allProgress)
            {
                var data = kvp.Value;
                Debug.Log($"  {data.label}: count={data.count}, correct={data.correctCount}, incorrect={data.incorrectCount}");
            }
        }

        // Public method for manual testing
        public void TestMarkCorrect(string label)
        {
            if (_learningProgress != null)
            {
                _learningProgress.RegisterWord(label);
                _learningProgress.MarkCorrect(label);
                Debug.Log($"[ObjectLearningProgressDebug] Marked '{label}' as CORRECT");
            }
        }

        // Public method for manual testing
        public void TestMarkIncorrect(string label)
        {
            if (_learningProgress != null)
            {
                _learningProgress.RegisterWord(label);
                _learningProgress.MarkIncorrect(label);
                Debug.Log($"[ObjectLearningProgressDebug] Marked '{label}' as INCORRECT");
            }
        }
    }
}
