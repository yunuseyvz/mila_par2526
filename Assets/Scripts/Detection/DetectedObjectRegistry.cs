using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DetectedObjectRegistry", menuName = "Detection/Detected Object Registry")]
public class DetectedObjectRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string Label;
        public float Confidence;
        public Vector3 Position;
        public float LastSeenTime;
    }

    [SerializeField] private List<Entry> entries = new();

    public IReadOnlyList<Entry> Entries => entries;

    public void Clear()
    {
        entries.Clear();
    }

    public void Upsert(string label, float confidence, Vector3 position, float distanceThreshold)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var now = Time.time;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!string.Equals(entry.Label, label, StringComparison.Ordinal))
            {
                continue;
            }

            if (Vector3.Distance(entry.Position, position) <= distanceThreshold)
            {
                entry.Position = position;
                entry.Confidence = confidence;
                entry.LastSeenTime = now;
                return;
            }
        }

        entries.Add(new Entry
        {
            Label = label,
            Confidence = confidence,
            Position = position,
            LastSeenTime = now
        });
    }
}
