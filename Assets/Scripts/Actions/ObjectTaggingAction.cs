using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LanguageTutor.Learning;
using LanguageTutor.Services.LLM;
using UnityEngine;

namespace LanguageTutor.Actions
{
    /// <summary>
    /// ObjectTagging action - tracks detected objects and saves progress to JSON.
    /// Simplified version: focus on JSON persistence, no highlighting yet.
    /// </summary>
    public class ObjectTaggingAction : ILLMAction
    {
        private readonly string _systemPrompt;
        private readonly DetectedObjectManager _objectManager;
        private readonly ObjectLearningProgress _learningProgress;

        public ObjectTaggingAction(
            string systemPrompt,
            DetectedObjectManager objectManager,
            ObjectLearningProgress learningProgress)
        {
            _systemPrompt = systemPrompt;
            _objectManager = objectManager;
            _learningProgress = learningProgress;
        }

        public string GetActionName() => "ObjectTagging";

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

            try
            {
                // Register detected objects in learning system
                if (_objectManager != null && _learningProgress != null)
                {
                    var objects = _objectManager.GetObjectLabels();
                    if (objects != null)
                    {
                        foreach (var label in objects)
                        {
                            _learningProgress.RegisterWord(label);
                        }
                        Debug.Log($"[ObjectTaggingAction] Registered {objects.Count} objects for learning");
                    }
                }

                // Let LLM respond to user input
                string response = await llmService.GenerateResponseAsync(
                    context.UserInput,
                    systemPrompt,
                    context.ConversationHistory);

                return LLMActionResult.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ObjectTaggingAction] Failed: {ex.Message}");
                return LLMActionResult.CreateFailure($"ObjectTagging failed: {ex.Message}");
            }
        }
    }
}
