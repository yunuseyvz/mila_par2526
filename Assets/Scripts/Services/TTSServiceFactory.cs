using System;
using UnityEngine;
using LanguageTutor.Data;

namespace LanguageTutor.Services
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
        public static ITTSService CreateService(TTSConfig config, MonoBehaviour coroutineRunner)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (coroutineRunner == null)
                throw new ArgumentNullException(nameof(coroutineRunner));

            switch (config.provider)
            {
                case TTSProvider.AllTalk:
                    Debug.Log("[TTSServiceFactory] Creating AllTalk service");
                    return new AllTalkService(config, coroutineRunner);

                case TTSProvider.HuggingFace:
                    Debug.Log("[TTSServiceFactory] Creating HuggingFace TTS service");
                    return new HuggingFaceTTSService(config, coroutineRunner);

                default:
                    throw new NotSupportedException($"TTS provider '{config.provider}' is not supported");
            }
        }
    }
}
