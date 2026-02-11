using System;
using System.Threading.Tasks;
using LanguageTutor.Services.LLM;

namespace LanguageTutor.Actions
{
    /// <summary>
    /// Conversation practice action for natural dialogue practice.
    /// Simulates real-world conversation scenarios.
    /// </summary>
    public class ConversationPracticeAction : ILLMAction
    {
        private readonly string _scenario;
        private readonly string _customSystemPrompt;
        private const string DEFAULT_CONVERSATION_PROMPT =
            @"You are a native speaker having a natural conversation in {0}. 
Scenario: {1}

Respond naturally as if you're really in this situation. Use appropriate idioms and expressions for your role. 
Keep the conversation flowing naturally. Match the user's language level - if they use simple language, respond simply. 
Be friendly and encouraging.";

        public ConversationPracticeAction(string scenario = "casual conversation", string customSystemPrompt = null)
        {
            _scenario = scenario;
            _customSystemPrompt = customSystemPrompt;
        }

        public string GetActionName() => "ConversationPractice";

        public bool CanExecute(LLMActionContext context)
        {
            return !string.IsNullOrWhiteSpace(context?.UserInput);
        }

        public async Task<LLMActionResult> ExecuteAsync(ILLMService llmService, LLMActionContext context)
        {
            try
            {
                string language = !string.IsNullOrEmpty(context.TargetLanguage)
                    ? context.TargetLanguage
                    : "English";

                string scenario = context.Parameters != null && context.Parameters.ContainsKey("scenario")
                    ? context.Parameters["scenario"].ToString()
                    : _scenario;

                // Use custom prompt if provided, otherwise use default template
                string systemPrompt = !string.IsNullOrEmpty(_customSystemPrompt)
                    ? _customSystemPrompt
                    : string.Format(DEFAULT_CONVERSATION_PROMPT, language, scenario);

                // Append additional context (e.g., room awareness) if provided
                if (!string.IsNullOrEmpty(context.SystemPrompt))
                {
                    systemPrompt += "\n\n" + context.SystemPrompt;
                }

                string response = await llmService.GenerateResponseAsync(
                    context.UserInput,
                    systemPrompt,
                    context.ConversationHistory);

                var result = LLMActionResult.CreateSuccess(response);
                result.Metadata["scenario"] = scenario;
                result.Metadata["target_language"] = language;

                return result;
            }
            catch (Exception ex)
            {
                return LLMActionResult.CreateFailure($"Conversation practice failed: {ex.Message}");
            }
        }
    }
}
