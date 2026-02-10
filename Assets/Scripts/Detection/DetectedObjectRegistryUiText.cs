using System.Text;
using TMPro;
using UnityEngine;

public class DetectedObjectRegistryUiText : MonoBehaviour
{
    [SerializeField] private DetectedObjectRegistry registry;
    [SerializeField] private TextMeshProUGUI textField;
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private int maxEntries = 12;

    private readonly StringBuilder _builder = new();
    private float _nextUpdateTime;

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
    }

    private void Update()
    {
        if (registry == null || textField == null)
        {
            return;
        }

        if (Time.time < _nextUpdateTime)
        {
            return;
        }

        _nextUpdateTime = Time.time + updateInterval;
        textField.text = BuildText();
    }

    private string BuildText()
    {
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
}
