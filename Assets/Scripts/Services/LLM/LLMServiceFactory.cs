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
        public static ILLMService CreateService(LLMSettings config, MonoBehaviour coroutineRunner)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (coroutineRunner == null)
                throw new ArgumentNullException(nameof(coroutineRunner));

            if (config.provider != LLMProvider.HuggingFace)
            {
                Debug.LogWarning($"[LLMServiceFactory] Provider '{config.provider}' is not supported. Using HuggingFace only.");
            }

            Debug.Log("[LLMServiceFactory] Creating HuggingFace service");
            return new HuggingFaceService(config, coroutineRunner);
        }
    }
}
