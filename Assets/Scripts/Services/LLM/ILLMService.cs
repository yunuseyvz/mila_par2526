using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LanguageTutor.Services.LLM
{
    /// <summary>
    /// Interface for Large Language Model service providers.
    /// Enables swappable LLM backends (Ollama, OpenAI, Azure, etc.)
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// Send a prompt to the LLM and receive a response asynchronously.
        /// </summary>
        /// <param name="prompt">The user input or query</param>
        /// <param name="conversationHistory">Optional conversation context for multi-turn dialogue</param>
        /// <returns>Task containing the LLM response text</returns>
        Task<string> GenerateResponseAsync(string prompt, List<ConversationMessage> conversationHistory = null);

        /// <summary>
        /// Send a prompt with a specific system message override.
        /// </summary>
        /// <param name="prompt">The user input or query</param>
        /// <param name="systemPrompt">Custom system prompt for this specific request</param>
        /// <param name="conversationHistory">Optional conversation context</param>
        /// <returns>Task containing the LLM response text</returns>
        Task<string> GenerateResponseAsync(string prompt, string systemPrompt, List<ConversationMessage> conversationHistory = null);

        /// <summary>
        /// Send structured content parts (text + images) to the LLM.
        /// </summary>
        /// <param name="contentParts">Ordered content parts for the user message</param>
        /// <param name="systemPrompt">Custom system prompt for this specific request</param>
        /// <param name="conversationHistory">Optional conversation context</param>
        /// <returns>Task containing the LLM response text</returns>
        Task<string> GenerateResponseAsync(List<LLMContentPart> contentParts, string systemPrompt, List<ConversationMessage> conversationHistory = null);

        /// <summary>
        /// Check if the LLM service is available and responding.
        /// </summary>
        /// <returns>True if service is healthy, false otherwise</returns>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Get the current model name being used.
        /// </summary>
        string GetModelName();
    }

    /// <summary>
    /// Represents a message in a conversation with role and content.
    /// </summary>
    [Serializable]
    public class ConversationMessage
    {
        public MessageRole Role;
        public string Content;
        public List<LLMContentPart> ContentParts;
        public DateTime Timestamp;

        public ConversationMessage(MessageRole role, string content)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
        }

        public ConversationMessage(MessageRole role, List<LLMContentPart> contentParts)
        {
            Role = role;
            ContentParts = contentParts;
            Content = ExtractText(contentParts);
            Timestamp = DateTime.Now;
        }

        private static string ExtractText(List<LLMContentPart> contentParts)
        {
            if (contentParts == null || contentParts.Count == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < contentParts.Count; i++)
            {
                var part = contentParts[i];
                if (part != null && part.Type == LLMContentPartType.Text && !string.IsNullOrWhiteSpace(part.Text))
                {
                    if (sb.Length > 0)
                        sb.Append("\n");
                    sb.Append(part.Text);
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Structured content part for chat messages (text and image support).
    /// </summary>
    [Serializable]
    public class LLMContentPart
    {
        public LLMContentPartType Type;
        public string Text;
        public string ImageUrl;

        public static LLMContentPart TextPart(string text)
        {
            return new LLMContentPart
            {
                Type = LLMContentPartType.Text,
                Text = text
            };
        }

        public static LLMContentPart ImageUrlPart(string url)
        {
            return new LLMContentPart
            {
                Type = LLMContentPartType.ImageUrl,
                ImageUrl = url
            };
        }
    }

    public enum LLMContentPartType
    {
        Text,
        ImageUrl
    }

    /// <summary>
    /// Role of the message sender in the conversation.
    /// </summary>
    public enum MessageRole
    {
        System,
        User,
        Assistant
    }
}
