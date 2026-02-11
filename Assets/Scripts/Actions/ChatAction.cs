using System;
using System.Threading.Tasks;
using LanguageTutor.Services.LLM;

namespace LanguageTutor.Actions
{
    /// <summary>
    /// Basic chat action for conversational interactions.
    /// Sends user input to LLM with conversation history.
    /// </summary>
    public class ChatAction : ILLMAction
    {
        private readonly string _systemPrompt;

        public ChatAction(string systemPrompt = null)
        {
            _systemPrompt = systemPrompt;
        }

        public string GetActionName() => "Chat";

        public bool CanExecute(LLMActionContext context)
        {
            return !string.IsNullOrWhiteSpace(context?.UserInput);
        }

        public async Task<LLMActionResult> ExecuteAsync(ILLMService llmService, LLMActionContext context)
        {
            try
            {
                // Combine base system prompt with additional context (e.g., room context)
                string systemPrompt = _systemPrompt ?? string.Empty;

                if (!string.IsNullOrEmpty(context.SystemPrompt))
                {
                    // Append context to base prompt instead of replacing
                    systemPrompt = string.IsNullOrEmpty(systemPrompt)
                        ? context.SystemPrompt
                        : systemPrompt + "\n\n" + context.SystemPrompt;
                }

                string response;

                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    response = await llmService.GenerateResponseAsync(
                        context.UserInput,
                        systemPrompt,
                        context.ConversationHistory);
                }
                else
                {
                    response = await llmService.GenerateResponseAsync(
                        context.UserInput,
                        context.ConversationHistory);
                }

                return LLMActionResult.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                return LLMActionResult.CreateFailure($"Chat action failed: {ex.Message}");
            }
        }
    }
}
