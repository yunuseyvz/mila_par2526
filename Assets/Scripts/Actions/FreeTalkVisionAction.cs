using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LanguageTutor.Core;
using LanguageTutor.Services.LLM;
using UnityEngine;

namespace LanguageTutor.Actions
{
    /// <summary>
    /// Free talk action that captures a passthrough frame when a trigger phrase is detected.
    /// </summary>
    public class FreeTalkVisionAction : ILLMAction
    {
        private readonly string _systemPrompt;
        private readonly PassthroughFrameCapture _frameCapture;
        private readonly string _triggerPhrase;
        private const string FallbackTriggerPhrase = "what's this";

        public FreeTalkVisionAction(string systemPrompt, PassthroughFrameCapture frameCapture, string triggerPhrase = "what is this")
        {
            _systemPrompt = systemPrompt;
            _frameCapture = frameCapture;
            _triggerPhrase = string.IsNullOrWhiteSpace(triggerPhrase) ? "what is this" : triggerPhrase;
        }

        public string GetActionName() => "FreeTalkVision";

        public bool CanExecute(LLMActionContext context)
        {
            return !string.IsNullOrWhiteSpace(context?.UserInput);
        }

        public async Task<LLMActionResult> ExecuteAsync(ILLMService llmService, LLMActionContext context)
        {
            if (llmService == null)
                return LLMActionResult.CreateFailure("LLM service is null");

            if (context == null || string.IsNullOrWhiteSpace(context.UserInput))
                return LLMActionResult.CreateFailure("Missing user input");

            string systemPrompt = !string.IsNullOrEmpty(context.SystemPrompt)
                ? context.SystemPrompt
                : _systemPrompt;

            bool shouldCapture = ContainsTriggerPhrase(context.UserInput, _triggerPhrase);
            if (!shouldCapture || _frameCapture == null)
            {
                if (_frameCapture == null)
                {
                    Debug.LogWarning("[FreeTalkVisionAction] PassthroughFrameCapture is not set. Using text-only response.");
                }
                return await ExecuteTextOnlyAsync(llmService, context, systemPrompt);
            }

            try
            {
                Debug.Log("[FreeTalkVisionAction] Trigger phrase detected. Capturing passthrough frame...");
                string dataUrl = await _frameCapture.CaptureFrameDataUrlAsync();

                var parts = new List<LLMContentPart>
                {
                    LLMContentPart.TextPart(context.UserInput),
                    LLMContentPart.ImageUrlPart(dataUrl)
                };

                string response = await llmService.GenerateResponseAsync(parts, systemPrompt, context.ConversationHistory);
                return LLMActionResult.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FreeTalkVisionAction] Vision capture failed, falling back to text-only: {ex.Message}");
                return await ExecuteTextOnlyAsync(llmService, context, systemPrompt);
            }
        }

        private async Task<LLMActionResult> ExecuteTextOnlyAsync(ILLMService llmService, LLMActionContext context, string systemPrompt)
        {
            try
            {
                string response = !string.IsNullOrEmpty(systemPrompt)
                    ? await llmService.GenerateResponseAsync(context.UserInput, systemPrompt, context.ConversationHistory)
                    : await llmService.GenerateResponseAsync(context.UserInput, context.ConversationHistory);

                return LLMActionResult.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                return LLMActionResult.CreateFailure($"FreeTalk action failed: {ex.Message}");
            }
        }

        private bool ContainsTriggerPhrase(string input, string triggerPhrase)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(triggerPhrase))
                return false;

            string normalizedInput = Normalize(input);
            string normalizedTrigger = Normalize(triggerPhrase);
            if (normalizedInput.IndexOf(normalizedTrigger, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string normalizedFallback = Normalize(FallbackTriggerPhrase);
            return normalizedInput.IndexOf(normalizedFallback, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "\\s+", " ").Trim();
        }
    }
}
