using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class DetectedObjectRegistryUiText : MonoBehaviour
{
    [SerializeField] private DetectedObjectRegistry registry;
    [SerializeField] private TextMeshProUGUI textField;
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private int maxEntries = 12;
    [SerializeField] private bool readFromFile = false;
    [SerializeField] private string logFileName = "detections.txt";
    [SerializeField] private int maxFileLines = 30;

    private readonly StringBuilder _builder = new();
    private float _nextUpdateTime;
    private string _logFilePath;

    private void Reset()
    {
        textField = GetComponent<TextMeshProUGUI>();
    }

    private void Awake()
    {
        if (textField == null)
        {
            textField = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (readFromFile)
        {
            if (string.IsNullOrWhiteSpace(logFileName))
            {
                logFileName = "detections.txt";
            }
            _logFilePath = Path.Combine(Application.persistentDataPath, logFileName);
        }
    }

    private void Update()
    {
        if (textField == null)
        {
            return;
        }

        if (Time.time < _nextUpdateTime)
        {
            return;
        }

        _nextUpdateTime = Time.time + updateInterval;
        textField.text = readFromFile ? BuildTextFromFile() : BuildTextFromRegistry();
    }

    private string BuildTextFromRegistry()
    {
        if (registry == null)
        {
            return "Detections\n(No registry assigned)";
        }

        _builder.Clear();
        _builder.Append("Detections\n");

        var entries = registry.Entries;
        var count = Mathf.Min(entries.Count, maxEntries);
        for (var i = 0; i < count; i++)
        {
            var entry = entries[i];
            _builder.Append(i + 1);
            _builder.Append(") ");
            _builder.Append(entry.Label);
            if (entry.Confidence >= 0f)
            {
                _builder.Append(" (");
                _builder.Append(Mathf.RoundToInt(entry.Confidence * 100f));
                _builder.Append("%)");
            }
            _builder.Append("\n");
        }

        if (entries.Count > count)
        {
            _builder.Append("... ");
            _builder.Append(entries.Count - count);
            _builder.Append(" more");
        }

        return _builder.ToString();
    }

    private string BuildTextFromFile()
    {
        _builder.Clear();
        _builder.Append("Detections (File)\n");

        if (string.IsNullOrWhiteSpace(_logFilePath))
        {
            _builder.Append("No log file path set.");
            return _builder.ToString();
        }

        if (!File.Exists(_logFilePath))
        {
            _builder.Append("Log file not found.");
            return _builder.ToString();
        }

        try
        {
            var lines = File.ReadAllLines(_logFilePath);
            var start = Mathf.Max(0, lines.Length - maxFileLines);
            for (var i = start; i < lines.Length; i++)
            {
                _builder.AppendLine(lines[i]);
            }
        }
        catch
        {
            _builder.Append("Failed to read log file.");
        }

        return _builder.ToString();
    }
}
