using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using LanguageTutor.Data;
using LanguageTutor.Utilities;

namespace LanguageTutor.Services.TTS
{
    /// <summary>
    /// ElevenLabs TTS service implementation.
    /// Communicates with ElevenLabs API for high-quality speech synthesis.
    /// API Documentation: https://elevenlabs.io/docs/api-reference/text-to-speech/convert
    /// </summary>
    public class ElevenLabsService : ITTSService
    {
        private readonly TTSSettings _config;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Dictionary<string, AudioClip> _audioCache;
        private UnityWebRequest _currentRequest;
        private float _currentSpeed = 1.0f;

        // ElevenLabs API constants
        private const string API_BASE_URL = "https://api.elevenlabs.io/v1";

        public ElevenLabsService(TTSSettings config, MonoBehaviour coroutineRunner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
            _audioCache = new Dictionary<string, AudioClip>();
            _currentSpeed = _config.speechRate;

            if (string.IsNullOrEmpty(_config.apiKey))
            {
                Debug.LogWarning("[ElevenLabsService] API key is not set. Please configure it in the TTS settings.");
            }
        }

        public async Task<AudioClip> SynthesizeSpeechAsync(string text, string voiceName = null, string language = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be empty", nameof(text));

            if (string.IsNullOrEmpty(_config.apiKey))
                throw new InvalidOperationException("ElevenLabs API key is not configured");

            // Check cache
            string cacheKey = GetCacheKey(text, voiceName, language);
            if (_config.enableCaching && _audioCache.TryGetValue(cacheKey, out AudioClip cachedClip))
            {
                Debug.Log($"[ElevenLabsService] Using cached audio for: {text.Substring(0, Math.Min(30, text.Length))}...");
                return cachedClip;
            }

            var tcs = new TaskCompletionSource<AudioClip>();
            _coroutineRunner.StartCoroutine(GenerateAudioCoroutine(text, voiceName, language, cacheKey, tcs));
            return await tcs.Task;
        }

        public async Task<string[]> GetAvailableVoicesAsync()
        {
            // ElevenLabs voices endpoint: GET /v1/voices
            // For simplicity, returning the default voice
            // You can extend this to fetch voices from the API
            return await Task.FromResult(new[] { _config.DefaultVoice });
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_config.apiKey))
                    return false;

                // Simple test - try to synthesize a very short text
                var testClip = await SynthesizeSpeechAsync("Hi", null, null);
                return testClip != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ElevenLabsService] Service availability check failed: {ex.Message}");
                return false;
            }
        }

        public void CancelSynthesis()
        {
            if (_currentRequest != null && !_currentRequest.isDone)
            {
                _currentRequest.Abort();
                _currentRequest = null;
                Debug.Log("[ElevenLabsService] Synthesis cancelled");
            }
        }

        public void SetSpeed(float speed)
        {
            _currentSpeed = Mathf.Clamp(speed, 0.25f, 2.0f);
            Debug.Log($"[ElevenLabsService] Speed set to: {_currentSpeed}");
        }

        private System.Collections.IEnumerator GenerateAudioCoroutine(
            string text, 
            string voiceName, 
            string language,
            string cacheKey,
            TaskCompletionSource<AudioClip> tcs)
        {
            // Get voice ID (use provided or default)
            string voiceId = voiceName ?? _config.DefaultVoice;
            
            // Build the API endpoint with output format query parameter
            string apiUrl = $"{API_BASE_URL}/text-to-speech/{voiceId}?output_format={_config.outputFormat}";
            
            // Prepare request body according to ElevenLabs API
            var requestBody = new ElevenLabsRequest
            {
                text = text,
                model_id = _config.modelId,
                voice_settings = new ElevenLabsVoiceSettings
                {
                    speed = _currentSpeed
                }
            };

            string jsonBody = JsonUtility.ToJson(requestBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            Debug.Log($"[ElevenLabsService]   apiUrl: {apiUrl}");
            Debug.Log($"[ElevenLabsService]   jsonBody: {jsonBody}");
        
            
            _currentRequest = new UnityWebRequest(apiUrl, "POST");
            _currentRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            _currentRequest.downloadHandler = new DownloadHandlerBuffer();
            _currentRequest.timeout = _config.timeoutSeconds;
            
            // Set headers (order matches ElevenLabs API documentation)
            _currentRequest.SetRequestHeader("xi-api-key", _config.apiKey);
            _currentRequest.SetRequestHeader("Content-Type", "application/json");
            _currentRequest.certificateHandler = new BypassCertificateHandler();
            
            yield return _currentRequest.SendWebRequest();
            
            if (_currentRequest.result != UnityWebRequest.Result.Success)
            {
                string errorMessage = _currentRequest.error;
                string responseText = _currentRequest.downloadHandler?.text;
                
                Debug.LogError($"[ElevenLabsService] Generation failed: {errorMessage}");
                var errorResponse = JsonUtility.FromJson<ElevenLabsErrorResponse>(responseText);
                Debug.LogError($"[ElevenLabsService] Status: {errorResponse.detail.status}");
                Debug.LogError($"[ElevenLabsService] Message: {errorResponse.detail.message}");
                tcs.SetException(new Exception($"ElevenLabs generation failed: {errorMessage}"));
                _currentRequest = null;
                yield break;
            }
            
            // Get audio data from response
            byte[] audioData = _currentRequest.downloadHandler.data;
            
            if (audioData == null || audioData.Length == 0)
            {
                Debug.LogError("[ElevenLabsService] No audio data received");
                tcs.SetException(new Exception("No audio data received from ElevenLabs"));
                _currentRequest = null;
                yield break;
            }
            
            Debug.Log($"[ElevenLabsService] Audio data received: {audioData.Length} bytes");
            
            // Convert MP3 data to AudioClip
            // ElevenLabs returns MP3 by default, but Unity's AudioClip.Create expects PCM data
            // We'll save to a temporary file and load it
            string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, $"elevenlabs_{System.Guid.NewGuid()}.mp3");
            
            try
            {
                System.IO.File.WriteAllBytes(tempPath, audioData);
                Debug.Log($"[ElevenLabsService] Saved audio to: {tempPath}");
                
                // Load the MP3 file as AudioClip
                string fileUrl = "file:///" + tempPath.Replace("\\", "/");
                using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
                {
                    audioRequest.timeout = _config.timeoutSeconds;
                    
                    yield return audioRequest.SendWebRequest();
                    
                    if (audioRequest.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(audioRequest);
                        
                        if (clip != null)
                        {
                            Debug.Log($"[ElevenLabsService] Audio loaded! Length: {clip.length}s");
                            
                            // Cache the clip
                            if (_config.enableCaching)
                            {
                                CacheAudioClip(cacheKey, clip);
                            }
                            
                            tcs.SetResult(clip);
                        }
                        else
                        {
                            Debug.LogError("[ElevenLabsService] AudioClip is null");
                            tcs.SetException(new Exception("Failed to create AudioClip"));
                        }
                    }
                    else
                    {
                        Debug.LogError($"[ElevenLabsService] Failed to load audio: {audioRequest.error}");
                        tcs.SetException(new Exception($"Failed to load audio: {audioRequest.error}"));
                    }
                }
            }
            finally
            {
                // Clean up temporary file
                if (System.IO.File.Exists(tempPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ElevenLabsService] Failed to delete temp file: {ex.Message}");
                    }
                }
            }

            _currentRequest = null;
        }

        private string GetCacheKey(string text, string voice, string language)
        {
            return $"{text}_{voice ?? _config.DefaultVoice}_{language ?? _config.defaultLanguage}_{_currentSpeed:F2}";
        }

        private void CacheAudioClip(string key, AudioClip clip)
        {
            // Manage cache size
            if (_audioCache.Count >= _config.maxCacheSize)
            {
                // Remove oldest entry (simple FIFO strategy)
                var enumerator = _audioCache.GetEnumerator();
                enumerator.MoveNext();
                string oldestKey = enumerator.Current.Key;
                _audioCache.Remove(oldestKey);
            }

            _audioCache[key] = clip;
        }
        
        #region DTOs
        [Serializable]
        private class ElevenLabsRequest
        {
            public string text;
            public string model_id;
            public ElevenLabsVoiceSettings voice_settings;
        }

        [Serializable]
        private class ElevenLabsVoiceSettings
        {
            public float speed;
        }
        
        [Serializable]
        private class ElevenLabsErrorResponse
        {
            public ErrorDetail detail;
        }
        
        [Serializable]
        private class ErrorDetail
        {
            public string status;
            public string message;
        }
        #endregion
    }
}
