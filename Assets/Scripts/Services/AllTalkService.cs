using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using LanguageTutor.Data;

namespace LanguageTutor.Services
{
    /// <summary>
    /// AllTalk TTS service implementation.
    /// Communicates with local AllTalk TTS server for speech synthesis.
    /// </summary>
    public class AllTalkService : ITTSService
    {
        private readonly TTSConfig _config;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Dictionary<string, AudioClip> _audioCache;
        private UnityWebRequest _currentRequest;

        public AllTalkService(TTSConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
            _audioCache = new Dictionary<string, AudioClip>();
        }

        public async Task<AudioClip> SynthesizeSpeechAsync(string text, string voiceName = null, string language = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be empty", nameof(text));

            // Check cache
            string cacheKey = GetCacheKey(text, voiceName, language);
            if (_config.enableCaching && _audioCache.TryGetValue(cacheKey, out AudioClip cachedClip))
            {
                Debug.Log($"[AllTalkService] Using cached audio for: {text.Substring(0, Math.Min(30, text.Length))}...");
                return cachedClip;
            }

            var tcs = new TaskCompletionSource<AudioClip>();
            _coroutineRunner.StartCoroutine(GenerateAudioCoroutine(text, voiceName, language, cacheKey, tcs));
            return await tcs.Task;
        }

        public async Task<string[]> GetAvailableVoicesAsync()
        {
            // AllTalk typically has voices in a specific directory
            // This is a simplified implementation - extend based on AllTalk API
            return await Task.FromResult(new[] { _config.defaultVoice });
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                // Try a simple health check - synthesize a very short audio
                var testClip = await SynthesizeSpeechAsync("test", null, null);
                return testClip != null;
            }
            catch
            {
                return false;
            }
        }

        public void CancelSynthesis()
        {
            if (_currentRequest != null && !_currentRequest.isDone)
            {
                _currentRequest.Abort();
                _currentRequest = null;
                Debug.Log("[AllTalkService] Synthesis cancelled");
            }
        }

        private System.Collections.IEnumerator GenerateAudioCoroutine(
            string text, 
            string voiceName, 
            string language,
            string cacheKey,
            TaskCompletionSource<AudioClip> tcs)
        {
            // Build request according to AllTalk API docs
            string voice = voiceName ?? _config.defaultVoice;
            string lang = language ?? _config.defaultLanguage;
            string outputFile = $"unitytts{System.Guid.NewGuid():N}"; // No extension or special chars
            
            WWWForm form = new WWWForm();
            form.AddField("text_input", text);
            form.AddField("text_filtering", "standard");
            form.AddField("character_voice_gen", voice);
            form.AddField("narrator_enabled", "false");
            form.AddField("narrator_voice_gen", voice);
            form.AddField("text_not_inside", "character");
            form.AddField("language", lang);
            form.AddField("output_file_name", outputFile);
            form.AddField("output_file_timestamp", "true");
            form.AddField("autoplay", "false");
            form.AddField("autoplay_volume", "0.8");
            
            string generateUrl = _config.serviceUrl.TrimEnd('/') + "/api/tts-generate";
            
            Debug.Log($"[AllTalkService] Generating TTS:");
            Debug.Log($"[AllTalkService]   URL: {generateUrl}");
            Debug.Log($"[AllTalkService]   Text: {text.Substring(0, Math.Min(50, text.Length))}...");
            Debug.Log($"[AllTalkService]   Voice: {voice}");
            
            _currentRequest = UnityWebRequest.Post(generateUrl, form);
            _currentRequest.timeout = _config.timeoutSeconds;
            _currentRequest.certificateHandler = new BypassCertificateHandler();
            
            yield return _currentRequest.SendWebRequest();
            
            if (_currentRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AllTalkService] Generation failed: {_currentRequest.error}");
                tcs.SetException(new Exception($"AllTalk generation failed: {_currentRequest.error}"));
                _currentRequest = null;
                yield break;
            }
            
            string responseText = _currentRequest.downloadHandler.text;
            Debug.Log($"[AllTalkService] Generation response: {responseText}");
            
            // Parse response according to docs
            AllTalkResponse response = JsonUtility.FromJson<AllTalkResponse>(responseText);
            
            if (response == null || response.status != "generate-success")
            {
                Debug.LogError($"[AllTalkService] Generation failed, status: {response?.status}");
                tcs.SetException(new Exception("AllTalk generation failed"));
                _currentRequest = null;
                yield break;
            }
            
            Debug.Log($"[AllTalkService] Generation successful:");
            Debug.Log($"[AllTalkService]   output_file_url: {response.output_file_url}");
            Debug.Log($"[AllTalkService]   output_file_path: {response.output_file_path}");
            
            // Construct full URL from relative path
            // According to docs: output_file_url is like "/audio/filename.wav"
            string audioUrl = _config.serviceUrl.TrimEnd('/') + response.output_file_url;
            Debug.Log($"[AllTalkService] Full audio URL: {audioUrl}");
            
            // Wait a moment for file to be ready
            yield return new WaitForSeconds(0.3f);
            
            // Fetch the audio file
            using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.WAV))
            {
                audioRequest.timeout = _config.timeoutSeconds;
                audioRequest.certificateHandler = new BypassCertificateHandler();
                
                yield return audioRequest.SendWebRequest();
                
                Debug.Log($"[AllTalkService] Audio fetch result: {audioRequest.result}");
                
                if (audioRequest.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(audioRequest);
                    
                    if (clip != null)
                    {
                        Debug.Log($"[AllTalkService] Audio loaded! Length: {clip.length}s");
                        
                        // Cache the clip
                        if (_config.enableCaching)
                        {
                            CacheAudioClip(cacheKey, clip);
                        }
                        
                        tcs.SetResult(clip);
                    }
                    else
                    {
                        Debug.LogError("[AllTalkService] AudioClip is null");
                        tcs.SetException(new Exception("Failed to create AudioClip"));
                    }
                }
                else
                {
                    Debug.LogError($"[AllTalkService] Failed to load audio: {audioRequest.error}");
                    Debug.LogError($"[AllTalkService] Response code: {audioRequest.responseCode}");
                    tcs.SetException(new Exception($"Failed to load audio: {audioRequest.error}"));
                }
            }

            _currentRequest = null;
        }

        private string GetCacheKey(string text, string voice, string language)
        {
            return $"{text}_{voice ?? _config.defaultVoice}_{language ?? _config.defaultLanguage}";
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
        private class AllTalkResponse
        {
            public string status; // "generate-success" or "generate-failure"
            public string output_file_path;
            public string output_file_url;
            public string output_cache_url;
        }
        #endregion
    }
}
