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
        public DateTime Timestamp;

        public ConversationMessage(MessageRole role, string content)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
        }
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
