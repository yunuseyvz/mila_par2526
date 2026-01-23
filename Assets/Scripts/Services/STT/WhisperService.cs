using System;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;
using LanguageTutor.Data;

namespace LanguageTutor.Services.STT
{
    /// <summary>
    /// Whisper STT service implementation wrapper.
    /// Uses Whisper.Unity package for speech-to-text transcription.
    /// </summary>
    public class WhisperService : ISTTService
    {
        private readonly STTConfig _config;
        private readonly WhisperManager _whisperManager;
        private bool _isCancelled;

        public WhisperService(STTConfig config, WhisperManager whisperManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _whisperManager = whisperManager ?? throw new ArgumentNullException(nameof(whisperManager));
        }

        public async Task<string> TranscribeAsync(AudioClip audioClip, string language = null)
        {
            if (audioClip == null)
                throw new ArgumentNullException(nameof(audioClip));

            _isCancelled = false;

            try
            {
                // Use Whisper.Unity's async method
                var whisperResult = await _whisperManager.GetTextAsync(audioClip);

                if (_isCancelled)
                    throw new OperationCanceledException("Transcription was cancelled");

                if (string.IsNullOrWhiteSpace(whisperResult.Result))
                    throw new Exception("Whisper returned empty transcription");

                return whisperResult.Result.Trim();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Whisper transcription failed: {ex.Message}", ex);
            }
        }

        public async Task<TranscriptionResult> TranscribeWithConfidenceAsync(
            AudioClip audioClip, 
            string expectedText = null, 
            string language = null)
        {
            if (audioClip == null)
                throw new ArgumentNullException(nameof(audioClip));

            try
            {
                // Basic transcription
                string transcribedText = await TranscribeAsync(audioClip, language);

                // Calculate confidence based on various factors
                float confidence = CalculateConfidence(transcribedText, expectedText, audioClip);

                var result = new TranscriptionResult(transcribedText, confidence)
                {
                    Duration = audioClip.length,
                    Language = language ?? _config.defaultLanguage
                };

                // If expected text is provided, calculate pronunciation accuracy
                if (!string.IsNullOrEmpty(expectedText))
                {
                    result.Metadata["expected_text"] = expectedText;
                    result.Metadata["accuracy"] = CalculateTextSimilarity(transcribedText, expectedText);
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Transcription with confidence failed: {ex.Message}", ex);
            }
        }

        public async Task<bool> IsAvailableAsync()
        {
            // Whisper.Unity is locally run, so it's always available if initialized
            return await Task.FromResult(_whisperManager != null);
        }

        public void CancelTranscription()
        {
            _isCancelled = true;
            Debug.Log("[WhisperService] Transcription cancelled");
        }

        /// <summary>
        /// Calculate confidence score based on transcription quality indicators.
        /// This is a heuristic approach - actual confidence would come from Whisper's internal metrics.
        /// </summary>
        private float CalculateConfidence(string transcription, string expectedText, AudioClip audioClip)
        {
            float confidence = 1.0f;

            // Reduce confidence if transcription is very short
            if (transcription.Length < 3)
                confidence *= 0.5f;

            // Reduce confidence if audio is very short (might be incomplete)
            if (audioClip.length < 0.5f)
                confidence *= 0.7f;

            // If we have expected text, use similarity as confidence indicator
            if (!string.IsNullOrEmpty(expectedText))
            {
                float similarity = CalculateTextSimilarity(transcription, expectedText);
                confidence *= similarity;
            }

            return Mathf.Clamp01(confidence);
        }

        /// <summary>
        /// Calculate similarity between transcribed and expected text using Levenshtein distance.
        /// Returns a value between 0 (completely different) and 1 (identical).
        /// </summary>
        private float CalculateTextSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return 0f;

            text1 = text1.ToLower().Trim();
            text2 = text2.ToLower().Trim();

            if (text1 == text2)
                return 1.0f;

            int distance = LevenshteinDistance(text1, text2);
            int maxLength = Math.Max(text1.Length, text2.Length);
            
            if (maxLength == 0)
                return 1.0f;

            return 1.0f - ((float)distance / maxLength);
        }

        /// <summary>
        /// Calculate Levenshtein distance between two strings.
        /// </summary>
        private int LevenshteinDistance(string s1, string s2)
        {
            int[,] matrix = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= s2.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[s1.Length, s2.Length];
        }
    }
}
