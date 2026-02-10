using System;
using System.Globalization;
using UnityEngine;

#if MRUK_INSTALLED
using Meta.XR.BuildingBlocks.AIBlocks;
#endif

#if MRUK_INSTALLED
[RequireComponent(typeof(ObjectDetectionAgent), typeof(ObjectDetectionVisualizer))]
#endif
public class ObjectDetectionListRecorder : MonoBehaviour
{
    [SerializeField] private DetectedObjectRegistry registry;
    [SerializeField] private float duplicateDistanceThreshold = 0.35f;

#if MRUK_INSTALLED
    private ObjectDetectionAgent _agent;
    private ObjectDetectionVisualizer _visualizer;

    private void Awake()
    {
        _agent = GetComponent<ObjectDetectionAgent>();
        _visualizer = GetComponent<ObjectDetectionVisualizer>();
    }

    private void OnEnable()
    {
        if (_agent != null)
        {
            _agent.OnBoxesUpdated += HandleBoxesUpdated;
        }
    }

    private void OnDisable()
    {
        if (_agent != null)
        {
            _agent.OnBoxesUpdated -= HandleBoxesUpdated;
        }
    }

    private void HandleBoxesUpdated(System.Collections.Generic.List<BoxData> batch)
    {
        if (registry == null || _visualizer == null || batch == null)
        {
            return;
        }

        foreach (var b in batch)
        {
            if (!TryParseLabel(b.label, out var label, out var confidence))
            {
                label = b.label;
                confidence = -1f;
            }

            if (!_visualizer.TryProject(b.position.x, b.position.y, b.scale.x, b.scale.y,
                    out var worldPos, out _, out _))
            {
                continue;
            }

            registry.Upsert(label, confidence, worldPos, duplicateDistanceThreshold);
        }
    }

    private static bool TryParseLabel(string rawLabel, out string label, out float confidence)
    {
        label = rawLabel;
        confidence = -1f;

        if (string.IsNullOrWhiteSpace(rawLabel))
        {
            return false;
        }

        var lastSpace = rawLabel.LastIndexOf(' ');
        if (lastSpace <= 0 || lastSpace >= rawLabel.Length - 1)
        {
            return false;
        }

        var trailing = rawLabel[(lastSpace + 1)..];
        if (!float.TryParse(trailing, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        label = rawLabel[..lastSpace].TrimEnd();
        confidence = parsed;
        return true;
    }
#else
    private void OnEnable()
    {
        Debug.LogWarning("[ObjectDetectionListRecorder] MRUK is not installed. Detection list recording is disabled.");
    }
#endif
}
