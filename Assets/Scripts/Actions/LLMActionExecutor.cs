using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using LanguageTutor.Services.LLM;
using UnityEngine;

namespace LanguageTutor.Actions
{
    /// <summary>
    /// Orchestrates execution of LLM actions with error handling and retries.
    /// Central hub for all AI-powered interactions.
    /// </summary>
    public class LLMActionExecutor
    {
        private readonly ILLMService _llmService;
        private readonly int _maxRetries;
        private readonly float _retryDelaySeconds;

        public LLMActionExecutor(ILLMService llmService, int maxRetries = 2, float retryDelaySeconds = 1.0f)
        {
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _maxRetries = maxRetries;
            _retryDelaySeconds = retryDelaySeconds;
        }

        /// <summary>
        /// Execute an LLM action with automatic retry on failure.
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="context">Execution context</param>
        /// <returns>Task containing the action result</returns>
        public async Task<LLMActionResult> ExecuteAsync(ILLMAction action, LLMActionContext context)
        {
            if (action == null)
                return LLMActionResult.CreateFailure("Action cannot be null");

            if (!action.CanExecute(context))
                return LLMActionResult.CreateFailure($"Action '{action.GetActionName()}' cannot execute with the provided context");

            var stopwatch = Stopwatch.StartNew();
            int attemptCount = 0;
            Exception lastException = null;

            while (attemptCount <= _maxRetries)
            {
                try
                {
                    attemptCount++;
                    UnityEngine.Debug.Log($"[LLMActionExecutor] Executing action: {action.GetActionName()} (Attempt {attemptCount}/{_maxRetries + 1})");

                    var result = await action.ExecuteAsync(_llmService, context);
                    
                    stopwatch.Stop();
                    result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                    if (result.Success)
                    {
                        UnityEngine.Debug.Log($"[LLMActionExecutor] Action '{action.GetActionName()}' completed successfully in {result.ExecutionTimeMs}ms");
                        return result;
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[LLMActionExecutor] Action '{action.GetActionName()}' failed: {result.ErrorMessage}");
                        
                        if (attemptCount <= _maxRetries)
                        {
                            await Task.Delay((int)(_retryDelaySeconds * 1000));
                            continue;
                        }
                        
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    UnityEngine.Debug.LogError($"[LLMActionExecutor] Exception in action '{action.GetActionName()}': {ex.Message}");

                    if (attemptCount <= _maxRetries)
                    {
                        await Task.Delay((int)(_retryDelaySeconds * 1000));
                    }
                }
            }

            stopwatch.Stop();
            return LLMActionResult.CreateFailure($"Action failed after {attemptCount} attempts: {lastException?.Message ?? "Unknown error"}");
        }

        /// <summary>
        /// Execute multiple actions in sequence.
        /// </summary>
        /// <param name="actions">List of actions to execute</param>
        /// <param name="context">Shared execution context</param>
        /// <param name="stopOnFirstFailure">Stop executing if an action fails</param>
        /// <returns>List of results for each action</returns>
        public async Task<List<LLMActionResult>> ExecuteSequenceAsync(
            List<ILLMAction> actions, 
            LLMActionContext context, 
            bool stopOnFirstFailure = true)
        {
            var results = new List<LLMActionResult>();

            foreach (var action in actions)
            {
                var result = await ExecuteAsync(action, context);
                results.Add(result);

                if (!result.Success && stopOnFirstFailure)
                {
                    UnityEngine.Debug.LogWarning($"[LLMActionExecutor] Stopping sequence due to failure in action: {action.GetActionName()}");
                    break;
                }

                // Update context with latest response for chain of actions
                if (result.Success && !string.IsNullOrEmpty(result.ResponseText))
                {
                    context.ConversationHistory?.Add(new ConversationMessage(MessageRole.Assistant, result.ResponseText));
                }
            }

            return results;
        }

        /// <summary>
        /// Check if the LLM service is available before executing actions.
        /// </summary>
        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                return await _llmService.IsAvailableAsync();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LLMActionExecutor] Failed to check service availability: {ex.Message}");
                return false;
            }
        }
    }
}
