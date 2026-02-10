using System;
using System.Globalization;
using System.IO;
using System.Text;
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
    [SerializeField] private bool logToConsole = true;
    [SerializeField] private bool logDiagnostics = true;
    [SerializeField] private LanguageTutor.UI.EventLog eventLog;
    [SerializeField] private float uiLogInterval = 0.5f;
    [SerializeField] private bool logToFile = true;
    [SerializeField] private string logFileName = "detections.txt";
    [SerializeField] private float fileLogInterval = 1.0f;

#if MRUK_INSTALLED
    private ObjectDetectionAgent _agent;
    private ObjectDetectionVisualizer _visualizer;
    private readonly StringBuilder _logBuilder = new();
    private bool _loggedMissingDeps;
    private float _nextUiLogTime;
    private float _nextFileLogTime;
    private string _logFilePath;

    private void Awake()
    {
        _agent = GetComponent<ObjectDetectionAgent>();
        _visualizer = GetComponent<ObjectDetectionVisualizer>();
        if (eventLog == null)
        {
            eventLog = FindObjectOfType<LanguageTutor.UI.EventLog>(true);
            if (logDiagnostics && eventLog == null)
            {
                Debug.LogWarning("[ObjectDetectionListRecorder] EventLog not found in scene.");
            }
        }

        if (logToFile)
        {
            if (string.IsNullOrWhiteSpace(logFileName))
            {
                logFileName = "detections.txt";
            }
            _logFilePath = Path.Combine(Application.persistentDataPath, logFileName);
        }
    }

    private void Start()
    {
        if (logDiagnostics)
        {
            Debug.Log("[ObjectDetectionListRecorder] Ready. Awaiting detections...");
        }
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
        if (batch == null)
        {
            return;
        }

        if (registry == null || _visualizer == null)
        {
            if (logDiagnostics && !_loggedMissingDeps)
            {
                Debug.LogWarning($"[ObjectDetectionListRecorder] Missing refs. Registry set: {registry != null}, Visualizer set: {_visualizer != null}.");
                _loggedMissingDeps = true;
            }
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

        if (logToConsole)
        {
            Debug.Log(BuildLog());
        }
        else if (logDiagnostics)
        {
            Debug.Log($"[ObjectDetectionListRecorder] Updated registry. Entries: {registry.Entries.Count}");
        }

        if (eventLog != null && Time.time >= _nextUiLogTime)
        {
            _nextUiLogTime = Time.time + uiLogInterval;
            eventLog.LogInfo(BuildLog());
        }

        if (logToFile && Time.time >= _nextFileLogTime)
        {
            _nextFileLogTime = Time.time + fileLogInterval;
            TryAppendToFile(BuildLog());
        }
    }

    private void TryAppendToFile(string content)
    {
        if (string.IsNullOrWhiteSpace(_logFilePath) || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        try
        {
            File.AppendAllText(_logFilePath, content + System.Environment.NewLine);
        }
        catch (Exception e)
        {
            if (logDiagnostics)
            {
                Debug.LogWarning($"[ObjectDetectionListRecorder] Failed to write log file: {e.Message}");
            }
        }
    }

    private string BuildLog()
    {
        _logBuilder.Clear();
        _logBuilder.Append("[ObjectDetectionListRecorder] Registry entries: ");

        var entries = registry.Entries;
        _logBuilder.Append(entries.Count);
        _logBuilder.Append('\n');

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            _logBuilder.Append(i + 1);
            _logBuilder.Append(") ");
            _logBuilder.Append(entry.Label);
            if (entry.Confidence >= 0f)
            {
                _logBuilder.Append(" (");
                _logBuilder.Append(Mathf.RoundToInt(entry.Confidence * 100f));
                _logBuilder.Append("%)");
            }
            _logBuilder.Append(" @ ");
            _logBuilder.Append(entry.Position.ToString("F2"));
            _logBuilder.Append('\n');
        }

        return _logBuilder.ToString();
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
