using System;
using UnityEngine;
using LanguageTutor.Data;

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

            if (config.provider != TTSProvider.AllTalk)
            {
                Debug.LogWarning($"[TTSServiceFactory] Provider '{config.provider}' is not supported. Using AllTalk only.");
            }

            Debug.Log("[TTSServiceFactory] Creating AllTalk service");
            return new AllTalkService(config, coroutineRunner);
        }
    }
}
