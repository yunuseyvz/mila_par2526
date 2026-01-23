using System.Collections.Generic;
using System.Threading.Tasks;
using LanguageTutor.Services.LLM;

namespace LanguageTutor.Actions
{
    /// <summary>
    /// Base interface for all LLM actions.
    /// Implements the Command pattern for extensible AI behaviors.
    /// </summary>
    public interface ILLMAction
    {
        /// <summary>
        /// Execute the LLM action and return the result.
        /// </summary>
        /// <param name="llmService">The LLM service to use for generation</param>
        /// <param name="context">Execution context containing input and conversation history</param>
        /// <returns>Task containing the action result</returns>
        Task<LLMActionResult> ExecuteAsync(ILLMService llmService, LLMActionContext context);

        /// <summary>
        /// Get the name/type of this action for logging and debugging.
        /// </summary>
        string GetActionName();

        /// <summary>
        /// Validate if the action can be executed with the given context.
        /// </summary>
        /// <param name="context">The execution context</param>
        /// <returns>True if the action can execute, false otherwise</returns>
        bool CanExecute(LLMActionContext context);
    }

    /// <summary>
    /// Context passed to LLM actions containing all necessary data.
    /// </summary>
    public class LLMActionContext
    {
        /// <summary>
        /// The user's input text (from speech recognition or typed input)
        /// </summary>
        public string UserInput { get; set; }

        /// <summary>
        /// Full conversation history for context-aware responses
        /// </summary>
        public List<ConversationMessage> ConversationHistory { get; set; }

        /// <summary>
        /// Custom system prompt override for this specific action
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// Additional parameters for action-specific configuration
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Target language for language learning actions
        /// </summary>
        public string TargetLanguage { get; set; }

        /// <summary>
        /// User's proficiency level for adaptive responses
        /// </summary>
        public string UserLevel { get; set; }

        public LLMActionContext()
        {
            ConversationHistory = new List<ConversationMessage>();
            Parameters = new Dictionary<string, object>();
        }

        public LLMActionContext(string userInput) : this()
        {
            UserInput = userInput;
        }
    }

    /// <summary>
    /// Result returned by LLM action execution.
    /// </summary>
    public class LLMActionResult
    {
        /// <summary>
        /// Whether the action executed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The generated response text from the LLM
        /// </summary>
        public string ResponseText { get; set; }

        /// <summary>
        /// Error message if execution failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Additional metadata about the action execution
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Execution time in milliseconds
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        public LLMActionResult()
        {
            Metadata = new Dictionary<string, object>();
        }

        public static LLMActionResult CreateSuccess(string responseText)
        {
            return new LLMActionResult
            {
                Success = true,
                ResponseText = responseText
            };
        }

        public static LLMActionResult CreateFailure(string errorMessage)
        {
            return new LLMActionResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
