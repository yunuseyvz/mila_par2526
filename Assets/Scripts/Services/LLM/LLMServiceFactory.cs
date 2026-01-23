using System;
using UnityEngine;
using LanguageTutor.Data;

namespace LanguageTutor.Services.LLM
{
    /// <summary>
    /// Factory for creating LLM service instances based on configuration.
    /// </summary>
    public static class LLMServiceFactory
    {
        /// <summary>
        /// Create an LLM service instance based on the provider type in the config.
        /// </summary>
        /// <param name="config">The LLM configuration</param>
        /// <param name="coroutineRunner">MonoBehaviour to run coroutines</param>
        /// <returns>An ILLMService implementation</returns>
        public static ILLMService CreateService(LLMConfig config, MonoBehaviour coroutineRunner)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (coroutineRunner == null)
                throw new ArgumentNullException(nameof(coroutineRunner));

            switch (config.provider)
            {
                case LLMProvider.Ollama:
                    Debug.Log("[LLMServiceFactory] Creating Ollama service");
                    return new OllamaService(config, coroutineRunner);

                case LLMProvider.Gemini:
                    Debug.Log("[LLMServiceFactory] Creating Gemini service");
                    return new GeminiService(config, coroutineRunner);

                case LLMProvider.OpenAI:
                    Debug.LogWarning("[LLMServiceFactory] OpenAI provider not yet implemented. Falling back to Ollama.");
                    return new OllamaService(config, coroutineRunner);

                case LLMProvider.Azure:
                    Debug.LogWarning("[LLMServiceFactory] Azure provider not yet implemented. Falling back to Ollama.");
                    return new OllamaService(config, coroutineRunner);

                case LLMProvider.HuggingFace:
                    Debug.Log("[LLMServiceFactory] Creating HuggingFace service");
                    return new HuggingFaceService(config, coroutineRunner);

                default:
                    throw new NotSupportedException($"LLM provider '{config.provider}' is not supported");
            }
        }
    }
}
