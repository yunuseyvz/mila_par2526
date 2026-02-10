using System.Text;
using UnityEngine;

public class DetectedObjectRegistryDebugView : MonoBehaviour
{
    [SerializeField] private DetectedObjectRegistry registry;
    [SerializeField] private int maxEntries = 12;
    [SerializeField] private Vector2 padding = new(12f, 12f);
    [SerializeField] private Vector2 size = new(480f, 320f);

    private readonly StringBuilder _builder = new();

    private void OnGUI()
    {
        if (registry == null)
        {
            return;
        }

        var rect = new Rect(padding.x, padding.y, size.x, size.y);
        GUI.Box(rect, "Detections");

        var contentRect = new Rect(rect.x + 8f, rect.y + 24f, rect.width - 16f, rect.height - 32f);
        GUI.Label(contentRect, BuildText());
    }

    private string BuildText()
    {
        _builder.Clear();

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

            _builder.Append(" @ ");
            _builder.Append(entry.Position.ToString("F2"));
            _builder.Append('\n');
        }

        if (entries.Count > count)
        {
            _builder.Append("... ");
            _builder.Append(entries.Count - count);
            _builder.Append(" more\n");
        }

        return _builder.ToString();
    }
}
