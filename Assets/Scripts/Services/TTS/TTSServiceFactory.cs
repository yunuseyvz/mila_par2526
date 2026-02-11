using System;
using UnityEngine;
using LanguageTutor.Data;
using static LanguageTutor.Data.TTSSettings;

namespace LanguageTutor.Services.TTS
{
    /// <summary>
    /// Factory for creating TTS service instances based on configuration.
    /// </summary>
    public static class TTSServiceFactory
    {
        /// <summary>
        /// Create a TTS service instance based on the provider type in the config.
        /// </summary>
        /// <param name="config">The TTS configuration</param>
        /// <param name="coroutineRunner">MonoBehaviour to run coroutines</param>
        /// <returns>An ITTSService implementation</returns>
        public static ITTSService CreateService(TTSSettings config, MonoBehaviour coroutineRunner)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (coroutineRunner == null)
                throw new ArgumentNullException(nameof(coroutineRunner));

            if (config.provider != TTSProvider.ElevenLabs)
            {
                Debug.LogWarning($"[TTSServiceFactory] Provider '{config.provider}' is not supported. Using ElevenLabs only.");
            }

            Debug.Log("[TTSServiceFactory] Creating ElevenLabs service");
            return new ElevenLabsService(config, coroutineRunner);
        }
    }
}
