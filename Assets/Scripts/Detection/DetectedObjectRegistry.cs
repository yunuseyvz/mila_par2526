using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[CreateAssetMenu(fileName = "DetectedObjectRegistry", menuName = "Detection/Detected Object Registry")]
public class DetectedObjectRegistry : ScriptableObject
{
    private const string ExportFileName = "detected_object_registry.json";
    [NonSerialized] private bool _sessionExportInitialized;

    [Serializable]
    public class Entry
    {
        public string Label;
        public float Confidence;
        public Vector3 Position;
        public float LastSeenTime;
        
        // Anki-style spaced repetition counters
        public int DifficultyScore = 0;      // Higher = more difficult, needs more practice
        public int TimesReviewed = 0;        // Total quiz attempts
        public int CorrectAnswers = 0;       // Successful identifications
        public int IncorrectAnswers = 0;     // Failed identifications
        public float NextReviewTime = 0f;    // When to review next (for scheduling)
    }

    [SerializeField] private List<Entry> entries = new();

    [Serializable]
    private class ExportPayload
    {
        public List<Entry> entries;
    }

    public IReadOnlyList<Entry> Entries => entries;
    public string ExportFilePath => Path.Combine(Application.persistentDataPath, ExportFileName);

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureSessionExportInitialized();
    }

    public void Clear()
    {
        EnsureSessionExportInitialized();
        entries.Clear();
        SaveToJson();
    }

    public void Upsert(string label, float confidence, Vector3 position, float distanceThreshold)
    {
        EnsureSessionExportInitialized();

        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var now = Time.time;
        var firstMatchIndex = -1;

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!string.Equals(entry.Label, label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (firstMatchIndex < 0)
            {
                firstMatchIndex = i;
                continue;
            }

            entries.RemoveAt(i);
            i--;
        }

        if (firstMatchIndex >= 0)
        {
            var entry = entries[firstMatchIndex];
            entry.Label = label;
            entry.Position = position;
            entry.Confidence = confidence;
            entry.LastSeenTime = now;
            SaveToJson();
            return;
        }

        entries.Add(new Entry
        {
            Label = label,
            Confidence = confidence,
            Position = position,
            LastSeenTime = now
        });

        SaveToJson();
    }

    public void SaveToJson()
    {
        try
        {
            var payload = new ExportPayload { entries = entries };
            var json = JsonUtility.ToJson(payload, true);
            File.WriteAllText(ExportFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DetectedObjectRegistry] Failed to save JSON export: {e.Message}");
        }
    }

    private void EnsureSessionExportInitialized()
    {
        if (_sessionExportInitialized)
        {
            return;
        }

        _sessionExportInitialized = true;

        try
        {
            if (File.Exists(ExportFilePath))
            {
                File.Delete(ExportFilePath);
            }

            SaveToJson();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DetectedObjectRegistry] Failed to reset JSON export on startup: {e.Message}");
        }
    }
}
