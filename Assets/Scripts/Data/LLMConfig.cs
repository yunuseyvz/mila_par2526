using UnityEngine;

namespace LanguageTutor.Data
{
    /// <summary>
    /// LLM provider types.
    /// </summary>
    public enum LLMProvider
    {
        Ollama,
        Gemini,
        OpenAI,
        Azure,
        HuggingFace
    }

    /// <summary>
    /// Configuration for Large Language Model service.
    /// Create via: Assets -> Create -> Language Tutor -> LLM Config
    /// </summary>
    [CreateAssetMenu(fileName = "LLMConfig", menuName = "Language Tutor/LLM Config", order = 1)]
    public class LLMConfig : ScriptableObject
    {
        [Header("Provider Selection")]
        [Tooltip("Choose the LLM provider to use")]
        public LLMProvider provider = LLMProvider.Ollama;

        [Header("═══════════ LOCAL SERVICES (Ollama) ═══════════")]
        [Tooltip("Base URL for local Ollama service")]
        public string ollamaServiceUrl = "http://127.0.0.1:11434";

        [Tooltip("API endpoint path for Ollama")]
        public string ollamaEndpointPath = "/api/generate";

        [Tooltip("Local Ollama model name (e.g., llama3, mistral)")]
        public string ollamaModelName = "llama3";

        [Header("═══════════ API SERVICES ═══════════")]
        [Space(10)]
        [Header("HuggingFace API")]
        [Tooltip("HuggingFace API URL (e.g., https://router.huggingface.co/v1/chat/completions)")]
        public string huggingFaceApiUrl = "https://router.huggingface.co/v1/chat/completions";

        [Tooltip("HuggingFace model name (e.g., meta-llama/Llama-3-70b-chat-hf)")]
        public string huggingFaceModelName = "google/gemma-3-27b-it";

        [Space(10)]
        [Header("Gemini API")]
        [Tooltip("Gemini API URL")]
        public string geminiApiUrl = "https://generativelanguage.googleapis.com/v1beta";

        [Tooltip("Gemini model name (e.g., gemini-pro)")]
        public string geminiModelName = "gemini-pro";

        [Header("Authentication")]
        [Tooltip("API Key for cloud providers (HuggingFace, Gemini, OpenAI). Leave empty for local Ollama.")]
        [TextArea(1, 3)]
        public string apiKey = "";

        // Backward compatibility properties
        public string serviceUrl
        {
            get
            {
                return provider switch
                {
                    LLMProvider.Ollama => ollamaServiceUrl,
                    LLMProvider.HuggingFace => huggingFaceApiUrl,
                    LLMProvider.Gemini => geminiApiUrl,
                    _ => ollamaServiceUrl
                };
            }
        }

        public string endpointPath
        {
            get
            {
                return provider switch
                {
                    LLMProvider.Ollama => ollamaEndpointPath,
                    _ => ""
                };
            }
        }

        public string modelName
        {
            get
            {
                return provider switch
                {
                    LLMProvider.Ollama => ollamaModelName,
                    LLMProvider.HuggingFace => huggingFaceModelName,
                    LLMProvider.Gemini => geminiModelName,
                    _ => ollamaModelName
                };
            }
        }

        [Header("Request Settings")]
        [Tooltip("Maximum number of tokens in the response")]
        [Range(50, 4096)]
        public int maxTokens = 512;

        [Tooltip("Temperature for response randomness (0.0 = deterministic, 1.0 = creative)")]
        [Range(0.0f, 2.0f)]
        public float temperature = 0.7f;

        [Tooltip("Enable streaming responses (if supported by provider)")]
        public bool enableStreaming = false;

        [Header("Retry & Timeout")]
        [Tooltip("Request timeout in seconds")]
        [Range(5, 120)]
        public int timeoutSeconds = 30;

        [Tooltip("Number of retry attempts on failure")]
        [Range(0, 5)]
        public int maxRetries = 2;

        [Tooltip("Delay between retries in seconds")]
        [Range(1, 10)]
        public float retryDelaySeconds = 2.0f;

        [Header("System Prompts")]
        [Tooltip("Default system prompt for general conversation")]
        [TextArea(3, 6)]
        public string defaultSystemPrompt = "You are a helpful language learning assistant. Provide clear, concise responses that help the user practice the language. Keep responses to 1-2 sentences unless asked for more detail.";

        [Tooltip("System prompt for grammar correction mode")]
        [TextArea(3, 6)]
        public string grammarCorrectionPrompt = "You are a language tutor focused on grammar correction. When the user speaks, identify any grammatical errors and provide a corrected version with a brief explanation. Be encouraging and constructive.";

        [Tooltip("System prompt for vocabulary teaching mode")]
        [TextArea(3, 6)]
        public string vocabularyTeachingPrompt = "You are a vocabulary tutor. Help the user learn new words by providing definitions, example sentences, and usage tips. Make learning engaging and memorable.";

        [Tooltip("System prompt for conversation practice mode")]
        [TextArea(3, 6)]
        public string conversationPracticePrompt = "You are a native speaker engaging in casual conversation. Respond naturally as if you're having a real dialogue. Use appropriate idioms and expressions. Keep the conversation flowing naturally.";

        /// <summary>
        /// Get the full service URL (base + endpoint)
        /// </summary>
        public string GetFullUrl()
        {
            return serviceUrl.TrimEnd('/') + "/" + endpointPath.TrimStart('/');
        }
    }
}
