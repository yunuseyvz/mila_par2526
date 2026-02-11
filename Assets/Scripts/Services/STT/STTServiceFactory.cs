using System;
using UnityEngine;
using LanguageTutor.Data;
using Whisper;

namespace LanguageTutor.Services.STT
{
    /// <summary>
    /// Factory for creating STT service instances based on configuration.
    /// Similar to LLMServiceFactory, allows switching between different STT providers.
    /// </summary>
    public static class STTServiceFactory
    {
        /// <summary>
        /// Create an STT service instance based on the provider type in the config.
        /// </summary>
        /// <param name="config">The STT configuration</param>
        /// <param name="coroutineRunner">MonoBehaviour to run coroutines (required for API-based providers)</param>
        /// <param name="whisperManager">WhisperManager for local Whisper (optional, only needed for local Whisper provider)</param>
        /// <returns>An ISTTService implementation</returns>
        public static ISTTService CreateService(STTSettings config, MonoBehaviour coroutineRunner, WhisperManager whisperManager = null)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (coroutineRunner == null)
                throw new ArgumentNullException(nameof(coroutineRunner));

            if (config.provider != STTProvider.HuggingFace)
            {
                Debug.LogWarning($"[STTServiceFactory] Provider '{config.provider}' is not supported. Using HuggingFace only.");
            }

            Debug.Log("[STTServiceFactory] Creating HuggingFace STT service");
            return new HuggingFaceSTTService(config, coroutineRunner);
        }

        /// <summary>
        /// Check if the specified provider requires a WhisperManager reference.
        /// </summary>
        public static bool RequiresWhisperManager(STTProvider provider)
        {
            return false;
        }

        /// <summary>
        /// Check if the specified provider requires an API key.
        /// </summary>
        public static bool RequiresApiKey(STTProvider provider)
        {
            return true;
        }
    }
}
