using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Provides the tutor/NPC with access to detected objects in the room.
/// Allows the tutor to reference, teach about, and interact with physical objects
/// during conversations.
/// </summary>
public class TutorContextComponent : MonoBehaviour
{
    [SerializeField] private DetectedObjectRegistry registry;
    [SerializeField] private ObjectDetectionListRecorder recorder;
    [SerializeField] private bool enableDebugLogging = true;

    private List<DetectedObjectRegistry.Entry> _lastDetectionsSnapshot = new();
    private Dictionary<string, int> _labelCountCache = new();

    private void Awake()
    {
        if (recorder == null)
            recorder = FindObjectOfType<ObjectDetectionListRecorder>();

        // Get registry from the recorder (use the same registry instance)
        if (registry == null && recorder != null)
        {
            registry = recorder.Registry;
        }

        if (enableDebugLogging)
        {
            Debug.Log($"[TutorContextComponent] Initialized. Registry: {(registry != null ? "FOUND (" + registry.Entries.Count + " entries)" : "NULL")}, " +
                      $"Recorder: {(recorder != null ? "FOUND" : "NULL")}");
        }
    }

    private void OnEnable()
    {
        if (recorder != null)
        {
            recorder.OnDetectionsThisFrame += HandleDetectionsUpdated;
        }
    }

    private void OnDisable()
    {
        if (recorder != null)
        {
            recorder.OnDetectionsThisFrame -= HandleDetectionsUpdated;
        }
    }

    /// <summary>
    /// Called whenever new detections arrive from the detection pipeline.
    /// </summary>
    private void HandleDetectionsUpdated(IReadOnlyList<DetectedObjectRegistry.Entry> snapshot)
    {
        if (snapshot == null)
            return;

        _lastDetectionsSnapshot.Clear();
        _lastDetectionsSnapshot.AddRange(snapshot);
        RebuildLabelCache();

        if (enableDebugLogging && snapshot.Count > 0)
        {
            Debug.Log($"[TutorContextComponent] Detected {snapshot.Count} new objects this frame. " +
                      $"Total in registry: {GetAllDetectedObjects().Count}");
        }
    }

    private void RebuildLabelCache()
    {
        _labelCountCache.Clear();
        if (registry == null)
            return;

        foreach (var entry in registry.Entries)
        {
            if (!_labelCountCache.ContainsKey(entry.Label))
                _labelCountCache[entry.Label] = 0;
            _labelCountCache[entry.Label]++;
        }
    }

    // =========================================================================
    // PUBLIC API: Query detected objects
    // =========================================================================

    /// <summary>
    /// Get all currently detected objects in the scene.
    /// </summary>
    public IReadOnlyList<DetectedObjectRegistry.Entry> GetAllDetectedObjects()
    {
        return registry?.Entries ?? new List<DetectedObjectRegistry.Entry>();
    }

    /// <summary>
    /// Find all objects with a specific label (e.g., "chair", "table").
    /// </summary>
    public List<DetectedObjectRegistry.Entry> FindObjectsByLabel(string label)
    {
        var results = new List<DetectedObjectRegistry.Entry>();
        if (registry == null || string.IsNullOrWhiteSpace(label))
            return results;

        foreach (var entry in registry.Entries)
        {
            if (entry.Label.Equals(label, System.StringComparison.OrdinalIgnoreCase))
                results.Add(entry);
        }

        return results;
    }

    /// <summary>
    /// Get the count of objects with a specific label.
    /// </summary>
    public int CountObjectsByLabel(string label)
    {
        if (_labelCountCache.TryGetValue(label, out var count))
            return count;
        return 0;
    }

    /// <summary>
    /// Get the closest detected object to the player's position.
    /// Returns null if no objects are detected.
    /// </summary>
    public DetectedObjectRegistry.Entry GetClosestObject()
    {
        var objects = GetAllDetectedObjects();
        if (objects.Count == 0)
            return null;

        var playerPos = Camera.main?.transform.position ?? transform.position;
        var closest = objects[0];
        var minDist = Vector3.Distance(closest.Position, playerPos);

        for (int i = 1; i < objects.Count; i++)
        {
            var dist = Vector3.Distance(objects[i].Position, playerPos);
            if (dist < minDist)
            {
                minDist = dist;
                closest = objects[i];
            }
        }

        return closest;
    }

    /// <summary>
    /// Get the closest object of a specific type.
    /// </summary>
    public DetectedObjectRegistry.Entry GetClosestObjectOfType(string label)
    {
        var objects = FindObjectsByLabel(label);
        if (objects.Count == 0)
            return null;

        var playerPos = Camera.main?.transform.position ?? transform.position;
        var closest = objects[0];
        var minDist = Vector3.Distance(closest.Position, playerPos);

        for (int i = 1; i < objects.Count; i++)
        {
            var dist = Vector3.Distance(objects[i].Position, playerPos);
            if (dist < minDist)
            {
                minDist = dist;
                closest = objects[i];
            }
        }

        return closest;
    }

    /// <summary>
    /// Get a list of unique object labels detected in the scene.
    /// </summary>
    public List<string> GetDetectedObjectLabels()
    {
        return _labelCountCache.Keys.ToList();
    }

    // =========================================================================
    // CONTEXT GENERATION: Build natural language descriptions
    // =========================================================================

    /// <summary>
    /// Generate a context string describing all detected objects for LLM prompts.
    /// Useful for enriching the tutor's conversation context.
    /// </summary>
    public string BuildRoomContextDescription()
    {
        var objects = GetAllDetectedObjects();
        if (objects.Count == 0)
            return "No objects detected in the room.";

        var lines = new List<string> { $"Detected {objects.Count} objects in the room:" };

        // Group by label and count
        var grouped = objects.GroupBy(o => o.Label)
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var group in grouped)
        {
            var label = group.Key;
            var count = group.Count();
            var confidence = group.Average(e => e.Confidence >= 0 ? e.Confidence : 0.5f);

            if (count == 1)
                lines.Add($"  • 1 {label} (confidence: {(confidence * 100):F0}%)");
            else
                lines.Add($"  • {count} {label}s (avg confidence: {(confidence * 100):F0}%)");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Build a prompt suffix for the LLM that includes detected objects context.
    /// This helps the tutor reference physical objects in the room.
    /// </summary>
    public string BuildTutorSystemPromptContext()
    {
        var descr = BuildRoomContextDescription();

        if (GetAllDetectedObjects().Count == 0)
            return "The room is empty or objects cannot be detected yet.";

        return $@"You are in an interactive AR environment. {descr}

You can reference these detected objects in your teaching:
- Ask the student to identify or describe nearby objects
- Teach vocabulary related to detected furniture or items
- Use object positions to create spatial learning exercises
- Encourage the student to interact with or describe what they see

Personalize your responses by mentioning specific objects you detect nearby.";
    }

    /// <summary>
    /// Get a human-readable description of a single object.
    /// Useful for referring to specific objects in conversation.
    /// </summary>
    public string DescribeObject(DetectedObjectRegistry.Entry entry)
    {
        if (entry == null)
            return "unknown object";

        var confStr = entry.Confidence >= 0
            ? $" (confidence: {(entry.Confidence * 100):F0}%)"
            : "";

        return $"{entry.Label}{confStr} at position {entry.Position.ToString("F1")}";
    }

    // =========================================================================
    // TEACHING HELPERS: Generate interactive exercises
    // =========================================================================

    /// <summary>
    /// Generate a teaching prompt about a randomly selected object in the room.
    /// </summary>
    public string GenerateRandomObjectTeachingPrompt()
    {
        var obj = GetClosestObject();
        if (obj == null)
            return "I don't see any objects in the room yet. Let me wait for the room to load.";

        return $@"Now let's talk about that {obj.Label} nearby. 
Can you describe it? What color is it? What is it used for? 
In English, we call it a '{obj.Label}'.";
    }

    /// <summary>
    /// Generate a conversation starter about detected objects.
    /// </summary>
    public string GenerateRoomTourPrompt()
    {
        var labels = GetDetectedObjectLabels();
        if (labels.Count == 0)
            return "I'm waiting to see what's in this room. Objects should appear shortly.";

        var objectList = string.Join(", ", labels.Take(3));
        if (labels.Count > 3)
            objectList += $" and {labels.Count - 3} more objects";

        return $"I can see {objectList} in your room. Would you like to learn the English names for these objects?";
    }

    /// <summary>
    /// Get the most recently detected objects (for teaching newest items first).
    /// </summary>
    public List<DetectedObjectRegistry.Entry> GetRecentDetections(int count = 5)
    {
        var objects = GetAllDetectedObjects();
        return objects
            .OrderByDescending(o => o.LastSeenTime)
            .Take(count)
            .ToList();
    }

    // =========================================================================
    // STATE HELPERS
    // =========================================================================

    public int GetTotalDetectedObjectCount()
    {
        return GetAllDetectedObjects().Count;
    }

    public bool HasDetectedObjects()
    {
        return GetTotalDetectedObjectCount() > 0;
    }

    public bool HasObjectsOfType(string label)
    {
        return CountObjectsByLabel(label) > 0;
    }
}
