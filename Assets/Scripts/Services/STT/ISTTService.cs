using System;
using System.Threading.Tasks;
using UnityEngine;

namespace LanguageTutor.Services.STT
{
    /// <summary>
    /// Interface for Speech-to-Text service providers.
    /// Enables swappable STT backends (Whisper, Azure, Google, etc.)
    /// </summary>
    public interface ISTTService
    {
        /// <summary>
        /// Transcribe audio to text asynchronously.
        /// </summary>
        /// <param name="audioClip">The audio clip to transcribe</param>
        /// <param name="language">Optional language hint for better accuracy</param>
        /// <returns>Task containing the transcribed text</returns>
        Task<string> TranscribeAsync(AudioClip audioClip, string language = null);

        /// <summary>
        /// Transcribe audio with confidence scoring for pronunciation assessment.
        /// </summary>
        /// <param name="audioClip">The audio clip to transcribe</param>
        /// <param name="expectedText">Expected text for comparison (optional)</param>
        /// <param name="language">Optional language hint</param>
        /// <returns>Task containing transcription result with confidence scores</returns>
        Task<TranscriptionResult> TranscribeWithConfidenceAsync(AudioClip audioClip, string expectedText = null, string language = null);

        /// <summary>
        /// Check if the STT service is available and responding.
        /// </summary>
        /// <returns>True if service is healthy, false otherwise</returns>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Cancel any ongoing transcription.
        /// </summary>
        void CancelTranscription();
    }

    /// <summary>
    /// Result of speech-to-text transcription with additional metadata.
    /// </summary>
    [Serializable]
    public class TranscriptionResult
    {
        public string Text;
        public float Confidence; // 0.0 to 1.0
        public float Duration;
        public string Language;
        public WordSegment[] Words;
        public System.Collections.Generic.Dictionary<string, object> Metadata;

        public TranscriptionResult(string text, float confidence = 1.0f)
        {
            Text = text;
            Confidence = confidence;
            Metadata = new System.Collections.Generic.Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Individual word segment with timing and confidence for detailed pronunciation feedback.
    /// </summary>
    [Serializable]
    public class WordSegment
    {
        public string Word;
        public float StartTime;
        public float EndTime;
        public float Confidence;
    }
}
