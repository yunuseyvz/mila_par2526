using System;
using System.Threading.Tasks;
using LanguageTutor.Services.LLM;

namespace LanguageTutor.Actions
{
    /// <summary>
    /// Vocabulary teaching action for learning new words.
    /// Provides definitions, examples, and usage tips for vocabulary.
    /// </summary>
    public class VocabularyTeachAction : ILLMAction
    {
        private readonly string _targetLanguage;
        private readonly string _customSystemPrompt;
        private const string DEFAULT_VOCAB_PROMPT = 
            @"You are a vocabulary tutor teaching {0}. The user wants to learn about: '{1}'

Provide:
1. A clear definition
2. 2-3 example sentences showing proper usage
3. Any relevant synonyms or related words
4. A tip to remember this word

Make it engaging and memorable. Keep your response concise but informative.";

        public VocabularyTeachAction(string targetLanguage = "English", string customSystemPrompt = null)
        {
            _targetLanguage = targetLanguage;
            _customSystemPrompt = customSystemPrompt;
        }

        public string GetActionName() => "VocabularyTeach";

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
                    : string.Format(DEFAULT_VOCAB_PROMPT, language, context.UserInput);

                string response = await llmService.GenerateResponseAsync(
                    context.UserInput, 
                    prompt, 
                    context.ConversationHistory);

                var result = LLMActionResult.CreateSuccess(response);
                result.Metadata["word_or_phrase"] = context.UserInput;
                result.Metadata["target_language"] = language;

                return result;
            }
            catch (Exception ex)
            {
                return LLMActionResult.CreateFailure($"Vocabulary teaching failed: {ex.Message}");
            }
        }
    }
}
