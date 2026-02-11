using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages detected objects from the ObjectDetectionListRecorder.
/// Provides word lists for various game modes (Word Building, etc).
/// Deduplicates and filters object names for use in educational activities.
/// </summary>
public class DetectedObjectManager : MonoBehaviour
{
    [SerializeField] private ObjectDetectionListRecorder recorder;
    [SerializeField] private float deduplicationDistanceThreshold = 0.5f;
    [SerializeField] private int minWordLength = 3;  // Only words with 3+ chars for spelling

    private List<string> _cachedObjectLabels = new();
    private float _lastCacheTime = -999f;
    private const float CACHE_DURATION = 2f;

    private void Awake()
    {
        if (recorder == null)
            recorder = FindObjectOfType<ObjectDetectionListRecorder>();
    }

    /// <summary>
    /// Get unique object labels detected in the room.
    /// Deduplicates near-identical strings (e.g., "chair" vs "Chair").
    /// </summary>
    public List<string> GetObjectLabels()
    {
        // Use cache to avoid rebuilding every frame
        if (Time.time - _lastCacheTime < CACHE_DURATION && _cachedObjectLabels.Count > 0)
            return _cachedObjectLabels;

        _cachedObjectLabels.Clear();

        if (recorder?.Registry == null || recorder.Registry.Entries.Count == 0)
            return _cachedObjectLabels;

        var seenLabels = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        
        foreach (var entry in recorder.Registry.Entries)
        {
            string label = entry.Label?.Trim().ToLower() ?? "";
            
            // Skip empty or very short labels
            if (string.IsNullOrWhiteSpace(label) || label.Length < minWordLength)
                continue;

            // Skip if we've already added a similar label (case-insensitive)
            if (seenLabels.Contains(label))
                continue;

            _cachedObjectLabels.Add(label);
            seenLabels.Add(label);
        }

        _lastCacheTime = Time.time;
        return _cachedObjectLabels;
    }

    /// <summary>
    /// Get a random object label from detected objects.
    /// Used for spelling challenges, interactive learning, etc.
    /// </summary>
    public string GetRandomObjectLabel()
    {
        var labels = GetObjectLabels();
        if (labels.Count == 0)
            return "OBJECT";  // Fallback

        return labels[Random.Range(0, labels.Count)];
    }

    /// <summary>
    /// Get multiple random labels without repetition.
    /// Useful for sequencing multiple spelling words.
    /// </summary>
    public List<string> GetRandomObjectLabels(int count)
    {
        var labels = GetObjectLabels();
        var result = new List<string>();

        if (labels.Count == 0)
        {
            result.Add("OBJECT");
            return result;
        }

        // Shuffle and take requested count
        var shuffled = labels.OrderBy(_ => Random.value).Take(count).ToList();
        return shuffled;
    }

    /// <summary>
    /// Check if any objects have been detected yet.
    /// </summary>
    public bool HasDetectedObjects()
    {
        return GetObjectLabels().Count > 0;
    }

    /// <summary>
    /// Get total count of unique object labels.
    /// </summary>
    public int GetObjectCount()
    {
        return GetObjectLabels().Count;
    }

    /// <summary>
    /// Clear the cache (useful after significant scene changes).
    /// </summary>
    public void ClearCache()
    {
        _cachedObjectLabels.Clear();
        _lastCacheTime = -999f;
    }
}
