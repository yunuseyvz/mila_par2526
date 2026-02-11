using System;
using System.Threading.Tasks;
using LanguageTutor.Services.LLM;

namespace LanguageTutor.Actions
{
    /// <summary>
    /// Grammar correction action for language learning.
    /// Analyzes user input for grammatical errors and provides corrections.
    /// </summary>
    public class GrammarCheckAction : ILLMAction
    {
        private readonly string _targetLanguage;
        private readonly string _customSystemPrompt;
        private const string DEFAULT_GRAMMAR_PROMPT =
            @"You are a language tutor focused on grammar correction. The user is learning {0}.

Analyze the following text for grammatical errors:
'{1}'

If there are errors:
1. Provide the corrected version
2. Explain what was wrong
3. Be encouraging and constructive

If there are no errors, praise the user and confirm the grammar is correct.

Keep your response concise and clear.";

        public GrammarCheckAction(string targetLanguage = "English", string customSystemPrompt = null)
        {
            _targetLanguage = targetLanguage;
            _customSystemPrompt = customSystemPrompt;
        }

        public string GetActionName() => "GrammarCheck";

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
                    : _targetLanguage;

                // Use custom prompt if provided, otherwise use default template
                string prompt = !string.IsNullOrEmpty(_customSystemPrompt)
                    ? _customSystemPrompt
                    : string.Format(DEFAULT_GRAMMAR_PROMPT, language, context.UserInput);

                // Append additional context (e.g., room awareness) if provided
                if (!string.IsNullOrEmpty(context.SystemPrompt))
                {
                    prompt += "\n\n" + context.SystemPrompt;
                }

                string response = await llmService.GenerateResponseAsync(
                    context.UserInput,
                    prompt,
                    context.ConversationHistory);

                var result = LLMActionResult.CreateSuccess(response);
                result.Metadata["original_text"] = context.UserInput;
                result.Metadata["target_language"] = language;

                return result;
            }
            catch (Exception ex)
            {
                return LLMActionResult.CreateFailure($"Grammar check failed: {ex.Message}");
            }
        }
    }
}
