using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using LanguageTutor.Data;

namespace LanguageTutor.Services.LLM
{
    /// <summary>
    /// Google Gemini API service implementation.
    /// Communicates with Google's Gemini API for AI text generation.
    /// </summary>
    public class GeminiService : ILLMService
    {
        private readonly LLMConfig _config;
        private MonoBehaviour _coroutineRunner;
        private const string GEMINI_API_BASE = "https://generativelanguage.googleapis.com/v1beta/models/";

        public GeminiService(LLMConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));

            if (string.IsNullOrWhiteSpace(_config.apiKey))
            {
                Debug.LogWarning("[GeminiService] API key is not set. Please add your Gemini API key in the LLMConfig.");
            }
        }

        public string GetModelName() => _config.modelName;

        public async Task<string> GenerateResponseAsync(string prompt, List<ConversationMessage> conversationHistory = null)
        {
            return await GenerateResponseAsync(prompt, _config.defaultSystemPrompt, conversationHistory);
        }

        public async Task<string> GenerateResponseAsync(string prompt, string systemPrompt, List<ConversationMessage> conversationHistory = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

            if (string.IsNullOrWhiteSpace(_config.apiKey))
                throw new InvalidOperationException("API key is required for Gemini service. Please set it in the LLMConfig.");

            var tcs = new TaskCompletionSource<string>();

            _coroutineRunner.StartCoroutine(SendRequestCoroutine(prompt, systemPrompt, conversationHistory, tcs));

            return await tcs.Task;
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_config.apiKey))
                    return false;

                var testResponse = await GenerateResponseAsync("test", "Reply with 'ok'", null);
                return !string.IsNullOrEmpty(testResponse);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GeminiService] Availability check failed: {ex.Message}");
                return false;
            }
        }

        private System.Collections.IEnumerator SendRequestCoroutine(
            string prompt,
            string systemPrompt,
            List<ConversationMessage> conversationHistory,
            TaskCompletionSource<string> tcs)
        {
            // Build contents array with system prompt and conversation history
            var contents = BuildContents(prompt, systemPrompt, conversationHistory);

            var request = new GeminiRequest
            {
                contents = contents,
                generationConfig = new GeminiGenerationConfig
                {
                    temperature = _config.temperature,
                    maxOutputTokens = _config.maxTokens
                }
            };

            string json = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            // Construct the full URL with API key
            string modelName = string.IsNullOrEmpty(_config.modelName) ? "gemini-pro" : _config.modelName;
            string fullUrl = $"{GEMINI_API_BASE}{modelName}:generateContent?key={_config.apiKey}";

            Debug.Log($"[GeminiService] Sending request to Gemini API");
            Debug.Log($"[GeminiService] Model: {modelName}");
            Debug.Log($"[GeminiService] Prompt length: {prompt.Length} chars");

            using (UnityWebRequest webRequest = new UnityWebRequest(fullUrl, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = _config.timeoutSeconds;

                float startTime = Time.time;
                var operation = webRequest.SendWebRequest();

                // Manual timeout with progress tracking
                while (!operation.isDone)
                {
                    float elapsed = Time.time - startTime;
                    if (elapsed > _config.timeoutSeconds)
                    {
                        Debug.LogError($"[GeminiService] Request timed out after {elapsed}s");
                        webRequest.Abort();
                        tcs.SetException(new System.Exception($"Request timed out after {elapsed}s"));
                        yield break;
                    }

                    if (elapsed > 5f && Mathf.FloorToInt(elapsed) % 5 == 0)
                    {
                        Debug.Log($"[GeminiService] Still waiting... {elapsed:F1}s elapsed");
                    }

                    yield return null;
                }

                Debug.Log($"[GeminiService] Request completed in {Time.time - startTime:F2}s");
                Debug.Log($"[GeminiService] Response Code: {webRequest.responseCode}");

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<GeminiResponse>(webRequest.downloadHandler.text);

                        if (response.candidates == null || response.candidates.Length == 0)
                        {
                            Debug.LogError("[GeminiService] No candidates in response");
                            tcs.SetException(new Exception("No candidates in Gemini response"));
                            yield break;
                        }

                        var candidate = response.candidates[0];
                        if (candidate.content == null || candidate.content.parts == null || candidate.content.parts.Length == 0)
                        {
                            Debug.LogError("[GeminiService] No content parts in response");
                            tcs.SetException(new Exception("No content in Gemini response"));
                            yield break;
                        }

                        string responseText = candidate.content.parts[0].text;
                        Debug.Log($"[GeminiService] Response received: {responseText.Substring(0, Math.Min(100, responseText.Length))}...");
                        tcs.SetResult(responseText);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GeminiService] Failed to parse response: {ex.Message}");
                        Debug.LogError($"[GeminiService] Response body: {webRequest.downloadHandler.text}");
                        tcs.SetException(new Exception($"Failed to parse Gemini response: {ex.Message}"));
                    }
                }
                else
                {
                    string errorMsg = $"Gemini API request failed: {webRequest.error}";
                    if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                    {
                        errorMsg += $"\nResponse: {webRequest.downloadHandler.text}";
                    }
                    Debug.LogError($"[GeminiService] {errorMsg}");
                    tcs.SetException(new Exception(errorMsg));
                }
            }
        }

        private GeminiContent[] BuildContents(string prompt, string systemPrompt, List<ConversationMessage> conversationHistory)
        {
            var contentsList = new List<GeminiContent>();

            // Add system prompt as user message (Gemini doesn't have a system role)
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                contentsList.Add(new GeminiContent
                {
                    role = "user",
                    parts = new GeminiPart[]
                    {
                        new GeminiPart { text = $"Instructions: {systemPrompt}" }
                    }
                });
                contentsList.Add(new GeminiContent
                {
                    role = "model",
                    parts = new GeminiPart[]
                    {
                        new GeminiPart { text = "Understood. I'll follow these instructions." }
                    }
                });
            }

            // Add conversation history
            if (conversationHistory != null)
            {
                foreach (var message in conversationHistory)
                {
                    if (message.Role == MessageRole.System)
                        continue; // Skip system messages in history

                    string role = message.Role == MessageRole.User ? "user" : "model";
                    contentsList.Add(new GeminiContent
                    {
                        role = role,
                        parts = new GeminiPart[]
                        {
                            new GeminiPart { text = message.Content }
                        }
                    });
                }
            }

            // Add current prompt
            contentsList.Add(new GeminiContent
            {
                role = "user",
                parts = new GeminiPart[]
                {
                    new GeminiPart { text = prompt }
                }
            });

            return contentsList.ToArray();
        }
    }

    #region Gemini API Data Structures

    [Serializable]
    public class GeminiRequest
    {
        public GeminiContent[] contents;
        public GeminiGenerationConfig generationConfig;
    }

    [Serializable]
    public class GeminiContent
    {
        public string role;
        public GeminiPart[] parts;
    }

    [Serializable]
    public class GeminiPart
    {
        public string text;
    }

    [Serializable]
    public class GeminiGenerationConfig
    {
        public float temperature;
        public int maxOutputTokens;
    }

    [Serializable]
    public class GeminiResponse
    {
        public GeminiCandidate[] candidates;
    }

    [Serializable]
    public class GeminiCandidate
    {
        public GeminiContent content;
        public string finishReason;
    }

    #endregion
}
