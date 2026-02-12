using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages object identification quiz using Anki-style spaced repetition.
/// Highlights objects and tracks user performance for adaptive learning.
/// </summary>
public class ObjectQuizManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DetectedObjectRegistry registry;
    [SerializeField] private ObjectDetectionListRecorder recorder;
    [SerializeField] private ObjectQuizHighlighter highlighter;
    
    [Header("Quiz Settings")]
    [SerializeField] private float difficultyWeight = 2.0f;  // Higher = prioritize difficult objects more
    [SerializeField] private int maxDifficultyScore = 10;    // Cap for difficulty score
    [SerializeField] private float correctScoreDecrease = 1f; // How much to decrease on correct answer
    [SerializeField] private float incorrectScoreIncrease = 2f; // How much to increase on incorrect answer
    
    private DetectedObjectRegistry.Entry _currentQuizObject;
    private bool _isQuizActive;

    public bool IsQuizActive => _isQuizActive;
    public string CurrentObjectLabel => _currentQuizObject?.Label;

    private void Awake()
    {
        if (registry == null)
        {
            var detectionRecorder = FindObjectOfType<ObjectDetectionListRecorder>();
            if (detectionRecorder != null)
                registry = detectionRecorder.Registry;
        }
        
        if (recorder == null)
            recorder = FindObjectOfType<ObjectDetectionListRecorder>();
        
        if (highlighter == null)
            highlighter = FindObjectOfType<ObjectQuizHighlighter>();
    }

    // Note: Animation is now handled by ObjectQuizHighlighter automatically

    /// <summary>
    /// Start a new quiz by selecting an object based on difficulty scoring.
    /// Objects with higher difficulty scores are more likely to be selected.
    /// </summary>
    public bool StartQuiz()
    {
        if (_isQuizActive)
        {
            Debug.LogWarning("[ObjectQuizManager] Quiz already active. Call EndQuiz() first.");
            return false;
        }

        var candidate = SelectNextQuizObject();
        if (candidate == null)
        {
            Debug.LogWarning("[ObjectQuizManager] No objects available for quiz.");
            return false;
        }

        _currentQuizObject = candidate;
        _isQuizActive = true;
        
        // Use highlighter to show the object
        if (highlighter != null)
        {
            highlighter.HighlightObject(_currentQuizObject);
        }
        else
        {
            Debug.LogWarning("[ObjectQuizManager] ObjectQuizHighlighter not found!");
        }
        
        Debug.Log($"[ObjectQuizManager] Quiz started for object: {_currentQuizObject.Label} at {_currentQuizObject.Position}");
        return true;
    }

    /// <summary>
    /// End the current quiz and remove highlighting.
    /// </summary>
    public void EndQuiz()
    {
        if (!_isQuizActive)
            return;

        _isQuizActive = false;
        _currentQuizObject = null;
        
        // Clear highlight
        if (highlighter != null)
        {
            highlighter.ClearHighlight();
        }
        
        Debug.Log("[ObjectQuizManager] Quiz ended.");
    }

    /// <summary>
    /// User submitted an answer. Check if correct and update difficulty scores.
    /// </summary>
    /// <param name="userAnswer">The object name the user said</param>
    /// <returns>True if correct, false otherwise</returns>
    public bool SubmitAnswer(string userAnswer)
    {
        if (!_isQuizActive || _currentQuizObject == null)
        {
            Debug.LogWarning("[ObjectQuizManager] No active quiz to submit answer to.");
            return false;
        }

        bool isCorrect = IsAnswerCorrect(userAnswer, _currentQuizObject.Label);
        
        // Update statistics
        _currentQuizObject.TimesReviewed++;
        
        if (isCorrect)
        {
            _currentQuizObject.CorrectAnswers++;
            // Decrease difficulty (object is becoming easier)
            _currentQuizObject.DifficultyScore = Mathf.Max(0, 
                _currentQuizObject.DifficultyScore - Mathf.RoundToInt(correctScoreDecrease));
            
            Debug.Log($"[ObjectQuizManager] ✓ CORRECT! '{_currentQuizObject.Label}' difficulty: {_currentQuizObject.DifficultyScore}");
        }
        else
        {
            _currentQuizObject.IncorrectAnswers++;
            // Increase difficulty (object needs more practice)
            _currentQuizObject.DifficultyScore = Mathf.Min(maxDifficultyScore, 
                _currentQuizObject.DifficultyScore + Mathf.RoundToInt(incorrectScoreIncrease));
            
            Debug.Log($"[ObjectQuizManager] ✗ INCORRECT! Expected '{_currentQuizObject.Label}', got '{userAnswer}'. Difficulty: {_currentQuizObject.DifficultyScore}");
        }

        _currentQuizObject.NextReviewTime = Time.time + CalculateReviewDelay(_currentQuizObject.DifficultyScore);
        
        return isCorrect;
    }

    /// <summary>
    /// Select the next object for quiz using weighted random selection.
    /// Higher difficulty scores = higher probability of selection.
    /// </summary>
    private DetectedObjectRegistry.Entry SelectNextQuizObject()
    {
        if (registry == null || registry.Entries.Count == 0)
            return null;

        var candidates = registry.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Label))
            .ToList();

        if (candidates.Count == 0)
            return null;

        // Build weighted list based on difficulty scores
        var weightedList = new List<DetectedObjectRegistry.Entry>();
        
        foreach (var entry in candidates)
        {
            // Base weight of 1, plus additional weight based on difficulty
            int weight = 1 + Mathf.RoundToInt(entry.DifficultyScore * difficultyWeight);
            
            // Add this entry multiple times based on weight
            for (int i = 0; i < weight; i++)
            {
                weightedList.Add(entry);
            }
        }

        // Randomly select from weighted list
        return weightedList[Random.Range(0, weightedList.Count)];
    }

    /// <summary>
    /// Check if user's answer matches the expected label (case-insensitive, flexible matching).
    /// </summary>
    private bool IsAnswerCorrect(string userAnswer, string correctLabel)
    {
        if (string.IsNullOrWhiteSpace(userAnswer) || string.IsNullOrWhiteSpace(correctLabel))
            return false;

        // Normalize both strings
        string normalizedAnswer = userAnswer.Trim().ToLowerInvariant();
        string normalizedLabel = correctLabel.Trim().ToLowerInvariant();

        // Exact match
        if (normalizedAnswer == normalizedLabel)
            return true;

        // Check if one contains the other (e.g., "chair" matches "office chair")
        if (normalizedAnswer.Contains(normalizedLabel) || normalizedLabel.Contains(normalizedAnswer))
            return true;

        return false;
    }

    /// <summary>
    /// Calculate delay until next review based on difficulty (Anki-style).
    /// </summary>
    private float CalculateReviewDelay(int difficultyScore)
    {
        // High difficulty = short delay, low difficulty = long delay
        if (difficultyScore >= 7)
            return 30f;   // 30 seconds - review very soon
        if (difficultyScore >= 4)
            return 120f;  // 2 minutes
        if (difficultyScore >= 2)
            return 300f;  // 5 minutes
        
        return 600f;      // 10 minutes - well learned
    }

    // Highlighting is now handled by ObjectQuizHighlighter component

    private void OnDestroy()
    {
        if (highlighter != null)
        {
            highlighter.ClearHighlight();
        }
    }

    /// <summary>
    /// Get statistics for debugging/UI display.
    /// </summary>
    public string GetQuizStatistics()
    {
        if (registry == null || registry.Entries.Count == 0)
            return "No objects detected yet.";

        int totalObjects = registry.Entries.Count;
        int wellLearned = registry.Entries.Count(e => e.DifficultyScore <= 2);
        int needsPractice = registry.Entries.Count(e => e.DifficultyScore >= 5);
        
        return $"Objects: {totalObjects} | Well-learned: {wellLearned} | Needs practice: {needsPractice}";
    }
}
