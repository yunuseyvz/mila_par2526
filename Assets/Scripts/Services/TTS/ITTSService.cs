using System;
using System.Threading.Tasks;
using UnityEngine;

namespace LanguageTutor.Services.TTS
{
    /// <summary>
    /// Interface for Text-to-Speech service providers.
    /// Enables swappable TTS backends (AllTalk, Azure, Google, etc.)
    /// </summary>
    public interface ITTSService
    {
        /// <summary>
        /// Convert text to speech and return an AudioClip.
        /// </summary>
        /// <param name="text">The text to synthesize</param>
        /// <param name="voiceName">Optional voice name override</param>
        /// <param name="language">Optional language code override (e.g., "en", "de", "es")</param>
        /// <returns>Task containing the generated AudioClip</returns>
        Task<AudioClip> SynthesizeSpeechAsync(string text, string voiceName = null, string language = null);

        /// <summary>
        /// Get list of available voices for the current configuration.
        /// </summary>
        /// <returns>Array of available voice identifiers</returns>
        Task<string[]> GetAvailableVoicesAsync();

        /// <summary>
        /// Check if the TTS service is available and responding.
        /// </summary>
        /// <returns>True if service is healthy, false otherwise</returns>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Cancel any ongoing speech synthesis.
        /// </summary>
        void CancelSynthesis();

        /// <summary>
        /// Set the speed of the speech synthesis.
        /// </summary>
        /// <param name="speed">Speed multiplier (0.25 to 2.0)</param>
        void SetSpeed(float speed);
    }
}
