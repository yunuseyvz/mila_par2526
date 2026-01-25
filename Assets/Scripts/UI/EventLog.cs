using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.Text;

namespace LanguageTutor.UI
{
    /// <summary>
    /// Event log system that tracks all system events with timestamps.
    /// Used for debugging, monitoring, and displaying system status to users.
    /// </summary>
    public class EventLog : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private UnityEngine.UI.ScrollRect scrollRect;

        [Header("Log Settings")]
        [Tooltip("Maximum number of log entries to keep in memory")]
        [SerializeField] private int maxLogEntries = 100;
        
        [Tooltip("Show timestamps for each entry")]
        [SerializeField] private bool showTimestamps = true;
        
        [Tooltip("Timestamp format (e.g., HH:mm:ss or HH:mm:ss.fff for milliseconds)")]
        [SerializeField] private string timestampFormat = "HH:mm:ss";
        
        [Tooltip("Auto-scroll to bottom when new entries are added")]
        [SerializeField] private bool autoScrollToBottom = true;

        [Header("Text Formatting")]
        [Tooltip("Font size for log text")]
        [SerializeField] private float fontSize = 16f;
        
        [Tooltip("Color for info messages")]
        [SerializeField] private Color infoColor = Color.white;
        
        [Tooltip("Color for warning messages")]
        [SerializeField] private Color warningColor = new Color(1f, 0.92f, 0.016f); // Yellow
        
        [Tooltip("Color for error messages")]
        [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f); // Red
        
        [Tooltip("Color for success messages")]
        [SerializeField] private Color successColor = new Color(0.3f, 1f, 0.3f); // Green
        
        [Tooltip("Color for system messages")]
        [SerializeField] private Color systemColor = new Color(0.7f, 0.7f, 1f); // Light blue

        private List<LogEntry> _logEntries = new List<LogEntry>();
        
        public enum LogLevel
        {
            Info,
            Warning,
            Error,
            Success,
            System
        }

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Message { get; set; }
            public LogLevel Level { get; set; }
        }

        private void Awake()
        {
            if (logText == null)
            {
                Debug.LogError("[EventLog] LogText is not assigned!");
            }
            else
            {
                logText.fontSize = fontSize;
            }
        }

        /// <summary>
        /// Log an info message.
        /// </summary>
        public void LogInfo(string message)
        {
            AddLogEntry(message, LogLevel.Info);
        }

        /// <summary>
        /// Log a warning message.
        /// </summary>
        public void LogWarning(string message)
        {
            AddLogEntry(message, LogLevel.Warning);
        }

        /// <summary>
        /// Log an error message.
        /// </summary>
        public void LogError(string message)
        {
            AddLogEntry(message, LogLevel.Error);
        }

        /// <summary>
        /// Log a success message.
        /// </summary>
        public void LogSuccess(string message)
        {
            AddLogEntry(message, LogLevel.Success);
        }

        /// <summary>
        /// Log a system message.
        /// </summary>
        public void LogSystem(string message)
        {
            AddLogEntry(message, LogLevel.System);
        }

        /// <summary>
        /// Add a log entry to the display.
        /// </summary>
        private void AddLogEntry(string message, LogLevel level)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = message,
                Level = level
            };

            _logEntries.Add(entry);
            Debug.Log($"[EventLog] [{level}] {message}");

            // Remove old entries if exceeding max
            while (_logEntries.Count > maxLogEntries)
            {
                _logEntries.RemoveAt(0);
            }

            UpdateLogDisplay();
        }

        /// <summary>
        /// Update the log text display.
        /// </summary>
        private void UpdateLogDisplay()
        {
            if (logText == null) return;

            StringBuilder sb = new StringBuilder();
            
            for (int i = 0; i < _logEntries.Count; i++)
            {
                var entry = _logEntries[i];
                Color color = GetColorForLevel(entry.Level);
                string colorHex = ColorUtility.ToHtmlStringRGB(color);
                
                // Build the log line
                if (showTimestamps)
                {
                    string timestamp = entry.Timestamp.ToString(timestampFormat);
                    sb.Append($"<color=#808080>[{timestamp}]</color> ");
                }
                
                // Add level prefix
                string levelPrefix = GetLevelPrefix(entry.Level);
                sb.Append($"<color=#{colorHex}>{levelPrefix}</color> ");
                
                // Add message
                sb.Append($"<color=#{colorHex}>{entry.Message}</color>");
                
                // Add line break if not the last entry
                if (i < _logEntries.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            logText.text = sb.ToString();

            // Auto-scroll to bottom
            if (autoScrollToBottom && scrollRect != null && scrollRect.content != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// Get the color for a log level.
        /// </summary>
        private Color GetColorForLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Info:
                    return infoColor;
                case LogLevel.Warning:
                    return warningColor;
                case LogLevel.Error:
                    return errorColor;
                case LogLevel.Success:
                    return successColor;
                case LogLevel.System:
                    return systemColor;
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// Get the prefix for a log level.
        /// </summary>
        private string GetLevelPrefix(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Info:
                    return "[INFO]";
                case LogLevel.Warning:
                    return "[WARN]";
                case LogLevel.Error:
                    return "[ERROR]";
                case LogLevel.Success:
                    return "[SUCCESS]";
                case LogLevel.System:
                    return "[SYSTEM]";
                default:
                    return "[LOG]";
            }
        }

        /// <summary>
        /// Clear all log entries.
        /// </summary>
        public void ClearLog()
        {
            _logEntries.Clear();
            UpdateLogDisplay();
        }

        /// <summary>
        /// Export log entries as a formatted string.
        /// </summary>
        public string ExportLog()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== EVENT LOG EXPORT ===");
            sb.AppendLine($"Generated: {DateTime.Now}");
            sb.AppendLine($"Total Entries: {_logEntries.Count}");
            sb.AppendLine();

            foreach (var entry in _logEntries)
            {
                string timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                sb.AppendLine($"[{timestamp}] [{entry.Level}] {entry.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Save log to a file.
        /// </summary>
        public void SaveLogToFile(string filename = "event_log.txt")
        {
            try
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
                System.IO.File.WriteAllText(path, ExportLog());
                LogSuccess($"Log saved to: {path}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to save log: {ex.Message}");
            }
        }

        /// <summary>
        /// Set maximum number of log entries.
        /// </summary>
        public void SetMaxLogEntries(int count)
        {
            maxLogEntries = Mathf.Max(10, count);
            
            // Trim if necessary
            while (_logEntries.Count > maxLogEntries)
            {
                _logEntries.RemoveAt(0);
            }
            
            UpdateLogDisplay();
        }

        /// <summary>
        /// Enable or disable timestamps.
        /// </summary>
        public void SetShowTimestamps(bool show)
        {
            showTimestamps = show;
            UpdateLogDisplay();
        }

        /// <summary>
        /// Enable or disable auto-scroll.
        /// </summary>
        public void SetAutoScroll(bool enabled)
        {
            autoScrollToBottom = enabled;
        }
    }
}
