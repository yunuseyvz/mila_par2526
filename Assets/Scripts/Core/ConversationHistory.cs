using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LanguageTutor.Services.LLM;

namespace LanguageTutor.Core
{
    /// <summary>
    /// Manages conversation history for maintaining context across multiple turns.
    /// Supports automatic summarization and history trimming.
    /// </summary>
    public class ConversationHistory
    {
        private readonly List<ConversationMessage> _messages;
        private readonly int _maxHistoryLength;
        private readonly bool _autoSummarize;

        public IReadOnlyList<ConversationMessage> Messages => _messages.AsReadOnly();
        public int MessageCount => _messages.Count;

        public ConversationHistory(int maxHistoryLength = 20, bool autoSummarize = true)
        {
            _messages = new List<ConversationMessage>();
            _maxHistoryLength = maxHistoryLength;
            _autoSummarize = autoSummarize;
        }

        /// <summary>
        /// Add a user message to the conversation history.
        /// </summary>
        public void AddUserMessage(string content)
        {
            AddMessage(new ConversationMessage(MessageRole.User, content));
        }

        /// <summary>
        /// Add an assistant (AI) message to the conversation history.
        /// </summary>
        public void AddAssistantMessage(string content)
        {
            AddMessage(new ConversationMessage(MessageRole.Assistant, content));
        }

        /// <summary>
        /// Add a system message to the conversation history.
        /// </summary>
        public void AddSystemMessage(string content)
        {
            AddMessage(new ConversationMessage(MessageRole.System, content));
        }

        /// <summary>
        /// Add a message to the conversation history.
        /// </summary>
        public void AddMessage(ConversationMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _messages.Add(message);

            // Trim history if it exceeds max length
            if (_messages.Count > _maxHistoryLength)
            {
                TrimHistory();
            }
        }

        /// <summary>
        /// Get the most recent N messages from the history.
        /// </summary>
        public List<ConversationMessage> GetRecentMessages(int count)
        {
            if (count <= 0)
                return new List<ConversationMessage>();

            int startIndex = Math.Max(0, _messages.Count - count);
            return _messages.Skip(startIndex).ToList();
        }

        /// <summary>
        /// Get messages filtered by role.
        /// </summary>
        public List<ConversationMessage> GetMessagesByRole(MessageRole role)
        {
            return _messages.Where(m => m.Role == role).ToList();
        }

        /// <summary>
        /// Clear all messages from the history.
        /// </summary>
        public void Clear()
        {
            _messages.Clear();
            Debug.Log("[ConversationHistory] History cleared");
        }

        /// <summary>
        /// Get a summary of the conversation (message count, time span).
        /// </summary>
        public ConversationSummary GetSummary()
        {
            if (_messages.Count == 0)
            {
                return new ConversationSummary
                {
                    MessageCount = 0,
                    UserMessageCount = 0,
                    AssistantMessageCount = 0,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now,
                    Duration = TimeSpan.Zero
                };
            }

            return new ConversationSummary
            {
                MessageCount = _messages.Count,
                UserMessageCount = _messages.Count(m => m.Role == MessageRole.User),
                AssistantMessageCount = _messages.Count(m => m.Role == MessageRole.Assistant),
                StartTime = _messages.First().Timestamp,
                EndTime = _messages.Last().Timestamp,
                Duration = _messages.Last().Timestamp - _messages.First().Timestamp
            };
        }

        /// <summary>
        /// Get conversation as formatted text for display or logging.
        /// </summary>
        public string GetFormattedHistory(bool includeTimestamps = false)
        {
            var lines = new List<string>();

            foreach (var msg in _messages)
            {
                string role = msg.Role.ToString();
                string timestamp = includeTimestamps ? $"[{msg.Timestamp:HH:mm:ss}] " : "";
                lines.Add($"{timestamp}{role}: {msg.Content}");
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Export conversation history for persistence or analysis.
        /// </summary>
        public ConversationExport Export()
        {
            return new ConversationExport
            {
                Messages = _messages.ToArray(),
                Summary = GetSummary(),
                ExportTime = DateTime.Now
            };
        }

        /// <summary>
        /// Import conversation history from an export.
        /// </summary>
        public void Import(ConversationExport export)
        {
            if (export == null || export.Messages == null)
                throw new ArgumentNullException(nameof(export));

            _messages.Clear();
            _messages.AddRange(export.Messages);
            
            Debug.Log($"[ConversationHistory] Imported {export.Messages.Length} messages");
        }

        /// <summary>
        /// Trim history when it exceeds max length.
        /// Keeps recent messages and optionally creates a summary.
        /// </summary>
        private void TrimHistory()
        {
            if (_messages.Count <= _maxHistoryLength)
                return;

            int messagesToRemove = _messages.Count - _maxHistoryLength;

            if (_autoSummarize)
            {
                // Keep a summary message of the removed conversation
                var removedMessages = _messages.Take(messagesToRemove).ToList();
                string summary = CreateHistorySummary(removedMessages);
                
                _messages.RemoveRange(0, messagesToRemove);
                
                // Add summary as a system message at the beginning
                _messages.Insert(0, new ConversationMessage(MessageRole.System, summary));
            }
            else
            {
                // Simply remove oldest messages
                _messages.RemoveRange(0, messagesToRemove);
            }

            Debug.Log($"[ConversationHistory] Trimmed history. Current count: {_messages.Count}");
        }

        /// <summary>
        /// Create a text summary of removed messages.
        /// </summary>
        private string CreateHistorySummary(List<ConversationMessage> messages)
        {
            int userMessages = messages.Count(m => m.Role == MessageRole.User);
            int assistantMessages = messages.Count(m => m.Role == MessageRole.Assistant);
            
            return $"[Previous conversation summary: {userMessages} user messages, {assistantMessages} assistant responses]";
        }
    }

    /// <summary>
    /// Summary statistics of a conversation.
    /// </summary>
    [Serializable]
    public class ConversationSummary
    {
        public int MessageCount;
        public int UserMessageCount;
        public int AssistantMessageCount;
        public DateTime StartTime;
        public DateTime EndTime;
        public TimeSpan Duration;

        public override string ToString()
        {
            return $"Messages: {MessageCount} ({UserMessageCount} user, {AssistantMessageCount} assistant), Duration: {Duration.TotalMinutes:F1} minutes";
        }
    }

    /// <summary>
    /// Export format for conversation persistence.
    /// </summary>
    [Serializable]
    public class ConversationExport
    {
        public ConversationMessage[] Messages;
        public ConversationSummary Summary;
        public DateTime ExportTime;
    }
}
