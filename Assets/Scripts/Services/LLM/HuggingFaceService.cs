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
    /// HuggingFace Inference API service implementation.
    /// Communicates with HuggingFace's OpenAI-compatible router API for AI text generation.
    /// Supports models like google/gemma-3-27b-it, meta-llama/Llama-3-70b-chat-hf, etc.
    /// </summary>
    public class HuggingFaceService : ILLMService
    {
        private readonly LLMSettings _config;
        private MonoBehaviour _coroutineRunner;
        private const string HUGGINGFACE_API_BASE = "https://router.huggingface.co/v1/chat/completions";

        public HuggingFaceService(LLMSettings config, MonoBehaviour coroutineRunner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));

            if (string.IsNullOrWhiteSpace(_config.apiKey))
            {
                Debug.LogWarning("[HuggingFaceService] API key (HF_TOKEN) is not set. Please add your HuggingFace token in the LanguageTutorConfig.");
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
                throw new InvalidOperationException("API key (HF_TOKEN) is required for HuggingFace service. Please set it in the LanguageTutorConfig.");

            var tcs = new TaskCompletionSource<string>();

            _coroutineRunner.StartCoroutine(SendRequestCoroutine(prompt, systemPrompt, conversationHistory, tcs));

            return await tcs.Task;
        }

        public async Task<string> GenerateResponseAsync(List<LLMContentPart> contentParts, string systemPrompt, List<ConversationMessage> conversationHistory = null)
        {
            if (contentParts == null || contentParts.Count == 0)
                throw new ArgumentException("Content parts cannot be empty", nameof(contentParts));

            if (string.IsNullOrWhiteSpace(_config.apiKey))
                throw new InvalidOperationException("API key (HF_TOKEN) is required for HuggingFace service. Please set it in the LanguageTutorConfig.");

            var tcs = new TaskCompletionSource<string>();

            _coroutineRunner.StartCoroutine(SendRequestWithPartsCoroutine(contentParts, systemPrompt, conversationHistory, tcs));

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
                Debug.LogWarning($"[HuggingFaceService] Availability check failed: {ex.Message}");
                return false;
            }
        }

        private System.Collections.IEnumerator SendRequestCoroutine(
            string prompt,
            string systemPrompt,
            List<ConversationMessage> conversationHistory,
            TaskCompletionSource<string> tcs)
        {
            // Build messages array with system prompt and conversation history
            var messages = BuildMessages(prompt, systemPrompt, conversationHistory);
            yield return SendRequestCoroutine(messages, $"Prompt length: {prompt.Length} chars", tcs);
        }

        private System.Collections.IEnumerator SendRequestWithPartsCoroutine(
            List<LLMContentPart> contentParts,
            string systemPrompt,
            List<ConversationMessage> conversationHistory,
            TaskCompletionSource<string> tcs)
        {
            var messages = BuildMessages(contentParts, systemPrompt, conversationHistory);
            yield return SendRequestCoroutine(messages, "Structured content parts", tcs);
        }

        private System.Collections.IEnumerator SendRequestCoroutine(
            List<HuggingFaceMessage> messages,
            string requestInfo,
            TaskCompletionSource<string> tcs)
        {
            string modelName = string.IsNullOrEmpty(_config.modelName) ? "google/gemma-3-27b-it" : _config.modelName;

            string json = BuildRequestJson(modelName, messages, _config.temperature, _config.maxTokens);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            Debug.Log("[HuggingFaceService] Sending request to HuggingFace API");
            Debug.Log($"[HuggingFaceService] Model: {modelName}");
            if (!string.IsNullOrWhiteSpace(requestInfo))
            {
                Debug.Log($"[HuggingFaceService] {requestInfo}");
            }
            LogMessages(messages);

            using (UnityWebRequest webRequest = new UnityWebRequest(HUGGINGFACE_API_BASE, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", $"Bearer {_config.apiKey}");
                webRequest.timeout = _config.timeoutSeconds;

                float startTime = Time.time;
                var operation = webRequest.SendWebRequest();

                // Manual timeout with progress tracking
                while (!operation.isDone)
                {
                    float elapsed = Time.time - startTime;
                    if (elapsed > _config.timeoutSeconds)
                    {
                        Debug.LogError($"[HuggingFaceService] Request timed out after {elapsed}s");
                        webRequest.Abort();
                        tcs.SetException(new System.Exception($"Request timed out after {elapsed}s"));
                        yield break;
                    }

                    if (elapsed > 5f && Mathf.FloorToInt(elapsed) % 5 == 0)
                    {
                        Debug.Log($"[HuggingFaceService] Still waiting... {elapsed:F1}s elapsed");
                    }

                    yield return null;
                }

                Debug.Log($"[HuggingFaceService] Request completed in {Time.time - startTime:F2}s");
                Debug.Log($"[HuggingFaceService] Response Code: {webRequest.responseCode}");

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<HuggingFaceResponse>(webRequest.downloadHandler.text);

                        if (response.choices == null || response.choices.Length == 0)
                        {
                            Debug.LogError("[HuggingFaceService] No choices in response");
                            tcs.SetException(new Exception("No choices in HuggingFace response"));
                            yield break;
                        }

                        var choice = response.choices[0];
                        if (choice.message == null || string.IsNullOrEmpty(choice.message.content))
                        {
                            Debug.LogError("[HuggingFaceService] No message content in response");
                            tcs.SetException(new Exception("No message content in HuggingFace response"));
                            yield break;
                        }

                        string responseText = choice.message.content;
                        Debug.Log($"[HuggingFaceService] Response received: {responseText.Substring(0, Math.Min(100, responseText.Length))}...");
                        tcs.SetResult(responseText);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[HuggingFaceService] Failed to parse response: {ex.Message}");
                        Debug.LogError($"[HuggingFaceService] Response body: {webRequest.downloadHandler.text}");
                        tcs.SetException(new Exception($"Failed to parse HuggingFace response: {ex.Message}"));
                    }
                }
                else
                {
                    string errorMsg = $"HuggingFace API request failed: {webRequest.error}";
                    if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                    {
                        errorMsg += $"\nResponse: {webRequest.downloadHandler.text}";
                    }
                    Debug.LogError($"[HuggingFaceService] {errorMsg}");
                    tcs.SetException(new Exception(errorMsg));
                }
            }
        }

        private List<HuggingFaceMessage> BuildMessages(string prompt, string systemPrompt, List<ConversationMessage> conversationHistory)
        {
            var messagesList = new List<HuggingFaceMessage>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messagesList.Add(new HuggingFaceMessage
                {
                    role = "system",
                    contentParts = new List<LLMContentPart> { LLMContentPart.TextPart(systemPrompt) }
                });
            }

            if (conversationHistory != null)
            {
                foreach (var message in conversationHistory)
                {
                    if (message == null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(systemPrompt) && message.Role == MessageRole.System)
                        continue;

                    var parts = BuildContentParts(message);
                    if (parts.Count == 0)
                        continue;

                    messagesList.Add(new HuggingFaceMessage
                    {
                        role = MapRole(message.Role),
                        contentParts = parts
                    });
                }
            }

            messagesList.Add(new HuggingFaceMessage
            {
                role = "user",
                contentParts = new List<LLMContentPart> { LLMContentPart.TextPart(prompt) }
            });

            return messagesList;
        }

        private List<HuggingFaceMessage> BuildMessages(List<LLMContentPart> contentParts, string systemPrompt, List<ConversationMessage> conversationHistory)
        {
            var messagesList = new List<HuggingFaceMessage>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messagesList.Add(new HuggingFaceMessage
                {
                    role = "system",
                    contentParts = new List<LLMContentPart> { LLMContentPart.TextPart(systemPrompt) }
                });
            }

            if (conversationHistory != null)
            {
                foreach (var message in conversationHistory)
                {
                    if (message == null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(systemPrompt) && message.Role == MessageRole.System)
                        continue;

                    var parts = BuildContentParts(message);
                    if (parts.Count == 0)
                        continue;

                    messagesList.Add(new HuggingFaceMessage
                    {
                        role = MapRole(message.Role),
                        contentParts = parts
                    });
                }
            }

            var sanitizedParts = SanitizeContentParts(contentParts);
            if (sanitizedParts.Count == 0)
                throw new ArgumentException("Content parts cannot be empty", nameof(contentParts));

            messagesList.Add(new HuggingFaceMessage
            {
                role = "user",
                contentParts = sanitizedParts
            });

            return messagesList;
        }

        /// <summary>
        /// Builds the JSON request string manually to match HuggingFace API format.
        /// Content is formatted as an array of content objects with "type" and "text" fields.
        /// </summary>
        private string BuildRequestJson(string model, List<HuggingFaceMessage> messages, float temperature, int maxTokens)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            
            // Model
            sb.Append($"\"model\":\"{EscapeJsonString(model)}\",");
            
            // Messages array
            sb.Append("\"messages\":[");
            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                sb.Append("{");
                sb.Append($"\"role\":\"{EscapeJsonString(msg.role)}\",");
                
                // Content as array of content objects (HuggingFace API format)
                sb.Append("\"content\":[");
                bool appendedPart = false;
                foreach (var part in msg.contentParts)
                {
                    if (part == null)
                        continue;

                    if (part.Type == LLMContentPartType.Text)
                    {
                        if (string.IsNullOrWhiteSpace(part.Text))
                            continue;

                        if (appendedPart)
                            sb.Append(",");

                        sb.Append("{");
                        sb.Append("\"type\":\"text\",");
                        sb.Append($"\"text\":\"{EscapeJsonString(part.Text)}\"");
                        sb.Append("}");
                        appendedPart = true;
                    }
                    else if (part.Type == LLMContentPartType.ImageUrl)
                    {
                        if (string.IsNullOrWhiteSpace(part.ImageUrl))
                            continue;

                        if (appendedPart)
                            sb.Append(",");

                        sb.Append("{");
                        sb.Append("\"type\":\"image_url\",");
                        sb.Append("\"image_url\":{");
                        sb.Append($"\"url\":\"{EscapeJsonString(part.ImageUrl)}\"");
                        sb.Append("}");
                        sb.Append("}");
                        appendedPart = true;
                    }
                }
                sb.Append("]");
                
                sb.Append("}");
                if (i < messages.Count - 1)
                    sb.Append(",");
            }
            sb.Append("],");
            
            // Temperature
            sb.Append($"\"temperature\":{temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            
            // Max tokens
            sb.Append($"\"max_tokens\":{maxTokens},");
            
            // Stream (always false for this implementation)
            sb.Append("\"stream\":false");
            
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Escapes special characters in a string for JSON encoding.
        /// </summary>
        private string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            var sb = new StringBuilder();
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 32)
                            sb.Append($"\\u{(int)c:X4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private string MapRole(MessageRole role)
        {
            switch (role)
            {
                case MessageRole.System:
                    return "system";
                case MessageRole.Assistant:
                    return "assistant";
                default:
                    return "user";
            }
        }

        private List<LLMContentPart> BuildContentParts(ConversationMessage message)
        {
            var parts = new List<LLMContentPart>();

            if (message.ContentParts != null && message.ContentParts.Count > 0)
            {
                foreach (var part in message.ContentParts)
                {
                    if (part == null)
                        continue;

                    if (part.Type == LLMContentPartType.Text)
                    {
                        if (!string.IsNullOrWhiteSpace(part.Text))
                            parts.Add(LLMContentPart.TextPart(part.Text));
                    }
                    else if (part.Type == LLMContentPartType.ImageUrl)
                    {
                        if (!string.IsNullOrWhiteSpace(part.ImageUrl))
                            parts.Add(LLMContentPart.ImageUrlPart(part.ImageUrl));
                    }
                }
            }

            if (parts.Count == 0 && !string.IsNullOrWhiteSpace(message.Content))
            {
                parts.Add(LLMContentPart.TextPart(message.Content));
            }

            return parts;
        }

        private List<LLMContentPart> SanitizeContentParts(List<LLMContentPart> parts)
        {
            var sanitized = new List<LLMContentPart>();
            if (parts == null)
                return sanitized;

            foreach (var part in parts)
            {
                if (part == null)
                    continue;

                if (part.Type == LLMContentPartType.Text)
                {
                    if (!string.IsNullOrWhiteSpace(part.Text))
                        sanitized.Add(LLMContentPart.TextPart(part.Text));
                }
                else if (part.Type == LLMContentPartType.ImageUrl)
                {
                    if (!string.IsNullOrWhiteSpace(part.ImageUrl))
                        sanitized.Add(LLMContentPart.ImageUrlPart(part.ImageUrl));
                }
            }

            return sanitized;
        }

        private void LogMessages(List<HuggingFaceMessage> messages)
        {
            if (messages == null || messages.Count == 0)
                return;

            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                string preview = BuildContentPreview(msg.contentParts);
                Debug.Log($"[HuggingFaceService] Message[{i}] role={msg.role}, content={preview}");
            }
        }

        private string BuildContentPreview(List<LLMContentPart> parts)
        {
            if (parts == null || parts.Count == 0)
                return "(empty)";

            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part == null)
                    continue;

                if (part.Type == LLMContentPartType.Text && !string.IsNullOrWhiteSpace(part.Text))
                {
                    if (sb.Length > 0)
                        sb.Append(" | ");
                    sb.Append(TrimLog(part.Text, 200));
                }
                else if (part.Type == LLMContentPartType.ImageUrl && !string.IsNullOrWhiteSpace(part.ImageUrl))
                {
                    if (sb.Length > 0)
                        sb.Append(" | ");
                    sb.Append("[image_url: ").Append(TrimLog(part.ImageUrl, 120)).Append("]");
                }
            }

            return sb.Length == 0 ? "(empty)" : sb.ToString();
        }

        private string TrimLog(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLen)
                return value;

            return value.Substring(0, maxLen) + "...";
        }
    }

    /// <summary>
    /// Internal message class for building requests.
    /// </summary>
    internal class HuggingFaceMessage
    {
        public string role;
        public List<LLMContentPart> contentParts;
    }

    #region HuggingFace API Data Structures (OpenAI-compatible format)

    // Note: Unity's JsonUtility doesn't handle polymorphic content arrays well,
    // so we use manual JSON building for the request. These classes are for reference
    // and response parsing.

    [Serializable]
    public class HuggingFaceResponse
    {
        public string id;
        public string @object;
        public long created;
        public string model;
        public HuggingFaceChoice[] choices;
        public HuggingFaceUsage usage;
    }

    [Serializable]
    public class HuggingFaceChoice
    {
        public int index;
        public HuggingFaceResponseMessage message;
        public string finish_reason;
    }

    [Serializable]
    public class HuggingFaceResponseMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class HuggingFaceUsage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }

    #endregion
}
