using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using LanguageTutor.Data;
using LanguageTutor.Utilities;

namespace LanguageTutor.Services.STT
{
    /// <summary>
    /// HuggingFace Inference API implementation for Speech-to-Text.
    /// Uses Whisper models (openai/whisper-large-v3-turbo) via HuggingFace's inference endpoints.
    /// </summary>
    public class HuggingFaceSTTService : ISTTService
    {
        private readonly STTConfig _config;
        private readonly MonoBehaviour _coroutineRunner;
        private bool _isCancelled;

        // HuggingFace Inference API endpoint for Whisper
        private const string DEFAULT_MODEL = "openai/whisper-large-v3-turbo";
        private const string HUGGINGFACE_INFERENCE_BASE = "https://router.huggingface.co/hf-inference/models/";

        public HuggingFaceSTTService(STTConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));

            if (string.IsNullOrWhiteSpace(_config.apiKey))
            {
                Debug.LogWarning("[HuggingFaceSTTService] API key (HF_TOKEN) is not set. Please add your HuggingFace token in the STTConfig.");
            }

            Debug.Log($"[HuggingFaceSTTService] Initialized with model: {GetModelName()}");
        }

        /// <summary>
        /// Get the model name being used for transcription.
        /// </summary>
        public string GetModelName()
        {
            return string.IsNullOrEmpty(_config.whisperModelName) ? DEFAULT_MODEL : _config.whisperModelName;
        }

        public async Task<string> TranscribeAsync(AudioClip audioClip, string language = null)
        {
            if (audioClip == null)
                throw new ArgumentNullException(nameof(audioClip));

            if (string.IsNullOrWhiteSpace(_config.apiKey))
                throw new InvalidOperationException("API key (HF_TOKEN) is required for HuggingFace STT service. Please set it in the STTConfig.");

            _isCancelled = false;

            var tcs = new TaskCompletionSource<string>();
            _coroutineRunner.StartCoroutine(TranscribeCoroutine(audioClip, language, tcs));
            return await tcs.Task;
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
            try
            {
                if (string.IsNullOrWhiteSpace(_config.apiKey))
                    return false;

                // Simple check - we can't easily test without sending audio
                // Just verify we have the required configuration
                return !string.IsNullOrEmpty(_config.apiKey);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HuggingFaceSTTService] Availability check failed: {ex.Message}");
                return false;
            }
        }

        public void CancelTranscription()
        {
            _isCancelled = true;
            Debug.Log("[HuggingFaceSTTService] Transcription cancelled");
        }

        private IEnumerator TranscribeCoroutine(AudioClip audioClip, string language, TaskCompletionSource<string> tcs)
        {
            Debug.Log($"[HuggingFaceSTTService] Starting transcription: {AudioEncoder.GetAudioInfo(audioClip)}");

            // Prepare audio for API (convert to mono, resample, encode to WAV)
            byte[] audioBytes;
            try
            {
                audioBytes = AudioEncoder.PrepareForSTT(audioClip, 16000);
                Debug.Log($"[HuggingFaceSTTService] Audio encoded to WAV: {audioBytes.Length} bytes");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HuggingFaceSTTService] Failed to encode audio: {ex.Message}");
                tcs.SetException(new Exception($"Failed to encode audio: {ex.Message}"));
                yield break;
            }

            // Build API URL
            string modelName = GetModelName();
            string apiUrl = $"{HUGGINGFACE_INFERENCE_BASE}{modelName}";
            
            Debug.Log($"[HuggingFaceSTTService] Sending to: {apiUrl}");

            using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(audioBytes);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Authorization", $"Bearer {_config.apiKey}");
                webRequest.SetRequestHeader("Content-Type", "audio/wav");
                webRequest.timeout = _config.timeoutSeconds;

                float startTime = Time.time;
                var operation = webRequest.SendWebRequest();

                // Wait with timeout and cancellation check
                while (!operation.isDone)
                {
                    if (_isCancelled)
                    {
                        webRequest.Abort();
                        tcs.SetException(new OperationCanceledException("Transcription was cancelled"));
                        yield break;
                    }

                    float elapsed = Time.time - startTime;
                    if (elapsed > _config.timeoutSeconds)
                    {
                        Debug.LogError($"[HuggingFaceSTTService] Request timed out after {elapsed}s");
                        webRequest.Abort();
                        tcs.SetException(new Exception($"Request timed out after {elapsed}s"));
                        yield break;
                    }

                    if (elapsed > 3f && Mathf.FloorToInt(elapsed) % 3 == 0)
                    {
                        Debug.Log($"[HuggingFaceSTTService] Transcribing... {elapsed:F1}s elapsed");
                    }

                    yield return null;
                }

                Debug.Log($"[HuggingFaceSTTService] Request completed in {Time.time - startTime:F2}s");
                Debug.Log($"[HuggingFaceSTTService] Response Code: {webRequest.responseCode}");

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string responseText = webRequest.downloadHandler.text;
                        Debug.Log($"[HuggingFaceSTTService] Raw response: {responseText}");

                        // Parse the response - HuggingFace returns JSON with "text" field
                        var response = JsonUtility.FromJson<HuggingFaceSTTResponse>(responseText);
                        
                        if (response == null || string.IsNullOrWhiteSpace(response.text))
                        {
                            // Try parsing as simple string (some endpoints return plain text)
                            if (!responseText.StartsWith("{"))
                            {
                                tcs.SetResult(responseText.Trim());
                                yield break;
                            }

                            Debug.LogError("[HuggingFaceSTTService] Empty transcription result");
                            tcs.SetException(new Exception("Empty transcription result from HuggingFace"));
                            yield break;
                        }

                        string transcription = response.text.Trim();
                        Debug.Log($"[HuggingFaceSTTService] Transcription: {transcription}");
                        tcs.SetResult(transcription);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[HuggingFaceSTTService] Failed to parse response: {ex.Message}");
                        Debug.LogError($"[HuggingFaceSTTService] Response body: {webRequest.downloadHandler.text}");
                        tcs.SetException(new Exception($"Failed to parse response: {ex.Message}"));
                    }
                }
                else
                {
                    string errorMsg = $"HuggingFace STT API request failed: {webRequest.error}";
                    if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                    {
                        errorMsg += $"\nResponse: {webRequest.downloadHandler.text}";
                    }
                    Debug.LogError($"[HuggingFaceSTTService] {errorMsg}");
                    
                    // Check for specific error codes
                    if (webRequest.responseCode == 401)
                    {
                        tcs.SetException(new Exception("Invalid HuggingFace API key. Please check your HF_TOKEN."));
                    }
                    else if (webRequest.responseCode == 503)
                    {
                        tcs.SetException(new Exception("Model is loading. Please try again in a few seconds."));
                    }
                    else
                    {
                        tcs.SetException(new Exception(errorMsg));
                    }
                }
            }
        }

        /// <summary>
        /// Calculate confidence score based on transcription quality indicators.
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

    #region HuggingFace STT API Response

    [Serializable]
    public class HuggingFaceSTTResponse
    {
        public string text;
    }

    #endregion
}
