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
    /// Ollama LLM service implementation.
    /// Communicates with local Ollama server for AI text generation.
    /// </summary>
    public class OllamaService : ILLMService
    {
        private readonly LLMConfig _config;
        private MonoBehaviour _coroutineRunner;

        public OllamaService(LLMConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
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

            var tcs = new TaskCompletionSource<string>();

            _coroutineRunner.StartCoroutine(SendRequestCoroutine(prompt, systemPrompt, conversationHistory, tcs));

            return await tcs.Task;
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var testResponse = await GenerateResponseAsync("test", "Reply with 'ok'", null);
                return !string.IsNullOrEmpty(testResponse);
            }
            catch
            {
                return false;
            }
        }

        private System.Collections.IEnumerator SendRequestCoroutine(
            string prompt, 
            string systemPrompt, 
            List<ConversationMessage> conversationHistory,
            TaskCompletionSource<string> tcs)
        {
            // Build the complete prompt with system message and history
            string fullPrompt = BuildFullPrompt(prompt, systemPrompt, conversationHistory);

            var request = new OllamaRequest
            {
                model = _config.modelName,
                prompt = fullPrompt,
                stream = _config.enableStreaming,
                options = new OllamaOptions
                {
                    temperature = _config.temperature,
                    num_predict = _config.maxTokens
                }
            };

            string json = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            string fullUrl = _config.GetFullUrl();
            
            // DEBUG: Log request details
            Debug.Log($"[OllamaService] Sending request to: {fullUrl}");
            Debug.Log($"[OllamaService] Model: {_config.modelName}");
            Debug.Log($"[OllamaService] Prompt length: {fullPrompt.Length} chars");
            Debug.Log($"[OllamaService] Request JSON: {json}");

            using (UnityWebRequest webRequest = new UnityWebRequest(fullUrl, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Accept", "application/json");
                webRequest.timeout = _config.timeoutSeconds;
                
                // Disable certificate validation for local network (helps with IP addresses)
                webRequest.certificateHandler = new BypassCertificateHandler();
                
                // Don't follow redirects automatically
                webRequest.redirectLimit = 0;

                Debug.Log($"[OllamaService] Request timeout: {_config.timeoutSeconds}s");
                Debug.Log($"[OllamaService] Sending web request...");
                
                float startTime = Time.time;
                var operation = webRequest.SendWebRequest();
                
                // Manual timeout with progress tracking
                while (!operation.isDone)
                {
                    float elapsed = Time.time - startTime;
                    if (elapsed > _config.timeoutSeconds)
                    {
                        Debug.LogError($"[OllamaService] Manual timeout after {elapsed}s");
                        webRequest.Abort();
                        tcs.SetException(new System.Exception($"Request timed out after {elapsed}s"));
                        yield break;
                    }
                    
                    if (elapsed > 5f && Mathf.FloorToInt(elapsed) % 5 == 0)
                    {
                        Debug.Log($"[OllamaService] Still waiting... {elapsed:F1}s elapsed, progress: {operation.progress:P0}");
                    }
                    
                    yield return null;
                }
                
                Debug.Log($"[OllamaService] Request completed in {Time.time - startTime:F2}s");

                // DEBUG: Log response details
                Debug.Log($"[OllamaService] Response received!");
                Debug.Log($"[OllamaService] Result: {webRequest.result}");
                Debug.Log($"[OllamaService] Response Code: {webRequest.responseCode}");
                Debug.Log($"[OllamaService] Error: {webRequest.error}");

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[OllamaService] Response body: {webRequest.downloadHandler.text}");
                    
                    try
                    {
                        var response = JsonUtility.FromJson<OllamaResponse>(webRequest.downloadHandler.text);
                        
                        if (string.IsNullOrEmpty(response.response))
                        {
                            Debug.LogError("[OllamaService] Empty response from Ollama service");
                            tcs.SetException(new Exception("Empty response from Ollama service"));
                        }
                        else
                        {
                            Debug.Log($"[OllamaService] Success! Response length: {response.response.Length} chars");
                            Debug.Log($"[OllamaService] Response preview: {response.response.Substring(0, Mathf.Min(100, response.response.Length))}...");
                            tcs.SetResult(response.response);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[OllamaService] Failed to parse response: {ex.Message}");
                        Debug.LogError($"[OllamaService] Raw response: {webRequest.downloadHandler.text}");
                        tcs.SetException(new Exception($"Failed to parse Ollama response: {ex.Message}"));
                    }
                }
                else
                {
                    string errorMsg = $"Ollama request failed: {webRequest.error}";
                    if (webRequest.responseCode == 404)
                        errorMsg += " - Model not found. Run 'ollama pull " + _config.modelName + "'";
                    
                    Debug.LogError($"[OllamaService] {errorMsg}");
                    Debug.LogError($"[OllamaService] Response body: {webRequest.downloadHandler?.text}");
                    
                    tcs.SetException(new Exception(errorMsg));
                }
            }
        }

        private string BuildFullPrompt(string userPrompt, string systemPrompt, List<ConversationMessage> history)
        {
            var sb = new StringBuilder();

            // Use Ollama's recommended format with clear role separation
            // System prompt should be strongly emphasized
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                sb.AppendLine("### System Instructions ###");
                sb.AppendLine(systemPrompt);
                sb.AppendLine("### End System Instructions ###");
                sb.AppendLine();
                sb.AppendLine("You MUST follow the system instructions above in all your responses.");
                sb.AppendLine();
            }

            // Add conversation history (if any)
            if (history != null && history.Count > 0)
            {
                sb.AppendLine("### Conversation History ###");
                foreach (var msg in history)
                {
                    string role = msg.Role == MessageRole.User ? "Human" : "Assistant";
                    sb.AppendLine($"{role}: {msg.Content}");
                }
                sb.AppendLine("### End Conversation History ###");
                sb.AppendLine();
            }

            // Add current user prompt with clear role marker
            sb.AppendLine("### Current Message ###");
            sb.AppendLine($"Human: {userPrompt}");
            sb.AppendLine();
            sb.Append("Assistant:");

            return sb.ToString();
        }

        #region DTOs
        [Serializable]
        private class OllamaRequest
        {
            public string model;
            public string prompt;
            public bool stream;
            public OllamaOptions options;
        }

        [Serializable]
        private class OllamaOptions
        {
            public float temperature;
            public int num_predict;
        }

        [Serializable]
        private class OllamaResponse
        {
            public string response;
            public string model;
            public bool done;
        }
        #endregion
    }
    
    /// <summary>
    /// Bypass certificate validation for local network requests.
    /// Only use for development with local LLM servers.
    /// </summary>
    public class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true; // Accept all certificates for local development
        }
    }
}
