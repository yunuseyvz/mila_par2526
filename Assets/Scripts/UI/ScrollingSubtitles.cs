using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;

namespace LanguageTutor.UI
{
    /// <summary>
    /// Movie-style scrolling subtitle system that displays conversation history
    /// at the bottom of the screen with automatic scrolling.
    /// </summary>
    public class ScrollingSubtitles : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI subtitleText;
        
        [Header("Subtitle Settings")]
        [Tooltip("Maximum number of subtitle lines to display")]
        [SerializeField] private int maxVisibleLines = 5;
        
        [Tooltip("Keep messages visible until new ones arrive (otherwise they scroll based on time)")]
        [SerializeField] private bool keepUntilNewMessage = true;

        [Header("Text Formatting")]
        [Tooltip("Color for user messages")]
        [SerializeField] private Color userColor = new Color(0.4f, 0.7f, 1f); // Light blue
        
        [Tooltip("Color for tutor messages")]
        [SerializeField] private Color tutorColor = new Color(0.5f, 1f, 0.5f); // Light green
        
        [Tooltip("Color for status messages")]
        [SerializeField] private Color statusColor = new Color(1f, 1f, 0.6f); // Yellow
        
        [Tooltip("Color for error messages")]
        [SerializeField] private Color errorColor = new Color(1f, 0.4f, 0.4f); // Light red

        [Tooltip("Font size for subtitle text")]
        [SerializeField] private float fontSize = 24f;

        [Header("Text Color Override")]
        [SerializeField] private bool forceTextColor = true;
        [SerializeField] private Color forcedTextColor = Color.white;

        private List<SubtitleEntry> _subtitleHistory = new List<SubtitleEntry>();
        
        private class SubtitleEntry
        {
            public string Speaker { get; set; }
            public string Message { get; set; }
            public Color SpeakerColor { get; set; }
        }

        private void Awake()
        {
            if (subtitleText == null)
            {
                Debug.LogError("[ScrollingSubtitles] SubtitleText is not assigned!");
            }
            else
            {
                subtitleText.fontSize = fontSize;
                ApplyForcedTextColor();
            }
        }

        private void OnEnable()
        {
            ApplyForcedTextColor();
        }

        private void LateUpdate()
        {
            if (forceTextColor)
            {
                ApplyForcedTextColor();
            }
        }

        private void ApplyForcedTextColor()
        {
            if (!forceTextColor || subtitleText == null)
                return;

            subtitleText.color = forcedTextColor;
        }

        /// <summary>
        /// Add a user message to the subtitle display.
        /// </summary>
        public void AddUserMessage(string message)
        {
            AddSubtitle("User", message, userColor);
        }

        /// <summary>
        /// Add a tutor message to the subtitle display.
        /// </summary>
        public void AddTutorMessage(string message)
        {
            AddSubtitle("Tutor", message, tutorColor);
        }

        /// <summary>
        /// Add a status message to the subtitle display.
        /// </summary>
        public void AddStatusMessage(string message)
        {
            AddSubtitle("System", message, statusColor);
        }

        /// <summary>
        /// Add an error message to the subtitle display.
        /// </summary>
        public void AddErrorMessage(string message)
        {
            AddSubtitle("Error", message, errorColor);
        }

        /// <summary>
        /// Add a subtitle entry to the display.
        /// </summary>
        private void AddSubtitle(string speaker, string message, Color speakerColor)
        {
            var entry = new SubtitleEntry
            {
                Speaker = speaker,
                Message = message,
                SpeakerColor = speakerColor
            };

            _subtitleHistory.Add(entry);
            Debug.Log($"[ScrollingSubtitles] Added {speaker}: {message}. Total entries: {_subtitleHistory.Count}");

            UpdateSubtitleDisplay();
        }

        /// <summary>
        /// Update the subtitle text display with current history.
        /// </summary>
        private void UpdateSubtitleDisplay()
        {
            if (subtitleText == null) return;

            // Remove old entries if exceeding max visible lines (scroll effect)
            while (_subtitleHistory.Count > maxVisibleLines)
            {
                _subtitleHistory.RemoveAt(0);
                Debug.Log($"[ScrollingSubtitles] Scrolled out oldest message. Remaining: {_subtitleHistory.Count}");
            }

            // Build the subtitle text
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _subtitleHistory.Count; i++)
            {
                var entry = _subtitleHistory[i];
                string colorHex = ColorUtility.ToHtmlStringRGB(entry.SpeakerColor);
                
                // Format: "Speaker: Message"
                sb.Append($"<color=#{colorHex}><b>{entry.Speaker}:</b></color> {entry.Message}");
                
                // Add line break if not the last entry
                if (i < _subtitleHistory.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            subtitleText.text = sb.ToString();
        }

        /// <summary>
        /// Clear all subtitles.
        /// </summary>
        public void ClearSubtitles()
        {
            _subtitleHistory.Clear();
            UpdateSubtitleDisplay();
        }

        /// <summary>
        /// Set the maximum number of visible subtitle lines.
        /// </summary>
        public void SetMaxVisibleLines(int count)
        {
            maxVisibleLines = Mathf.Max(1, count);
            UpdateSubtitleDisplay();
        }
    }
}
