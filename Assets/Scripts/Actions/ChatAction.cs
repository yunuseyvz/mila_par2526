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
                string systemPrompt = !string.IsNullOrEmpty(context.SystemPrompt) 
                    ? context.SystemPrompt 
                    : _systemPrompt;

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
