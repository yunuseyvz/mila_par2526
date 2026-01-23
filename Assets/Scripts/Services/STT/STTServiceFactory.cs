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
        public static ISTTService CreateService(STTConfig config, MonoBehaviour coroutineRunner, WhisperManager whisperManager = null)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (coroutineRunner == null)
                throw new ArgumentNullException(nameof(coroutineRunner));

            switch (config.provider)
            {
                case STTProvider.WhisperLocal:
                    if (whisperManager == null)
                    {
                        Debug.LogError("[STTServiceFactory] WhisperManager is required for local Whisper provider!");
                        throw new ArgumentNullException(nameof(whisperManager), 
                            "WhisperManager is required for WhisperLocal provider. Assign it in the Inspector or switch to HuggingFace provider.");
                    }
                    Debug.Log("[STTServiceFactory] Creating local Whisper service");
                    return new WhisperService(config, whisperManager);

                case STTProvider.HuggingFace:
                    Debug.Log("[STTServiceFactory] Creating HuggingFace STT service");
                    return new HuggingFaceSTTService(config, coroutineRunner);

                case STTProvider.Azure:
                    Debug.LogWarning("[STTServiceFactory] Azure STT provider not yet implemented. Falling back to HuggingFace.");
                    return new HuggingFaceSTTService(config, coroutineRunner);

                case STTProvider.Google:
                    Debug.LogWarning("[STTServiceFactory] Google STT provider not yet implemented. Falling back to HuggingFace.");
                    return new HuggingFaceSTTService(config, coroutineRunner);

                case STTProvider.AWS:
                    Debug.LogWarning("[STTServiceFactory] AWS STT provider not yet implemented. Falling back to HuggingFace.");
                    return new HuggingFaceSTTService(config, coroutineRunner);

                default:
                    throw new NotSupportedException($"STT provider '{config.provider}' is not supported");
            }
        }

        /// <summary>
        /// Check if the specified provider requires a WhisperManager reference.
        /// </summary>
        public static bool RequiresWhisperManager(STTProvider provider)
        {
            return provider == STTProvider.WhisperLocal;
        }

        /// <summary>
        /// Check if the specified provider requires an API key.
        /// </summary>
        public static bool RequiresApiKey(STTProvider provider)
        {
            return provider == STTProvider.HuggingFace ||
                   provider == STTProvider.Azure ||
                   provider == STTProvider.Google ||
                   provider == STTProvider.AWS;
        }
    }
}
