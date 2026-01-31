using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using LanguageTutor.Data;

namespace LanguageTutor.Services.TTS
{
    public class HuggingFaceTTSService : ITTSService
    {
        private readonly TTSConfig _config;
        private readonly MonoBehaviour _coroutineRunner;
        private readonly Dictionary<string, AudioClip> _audioCache;
        private UnityWebRequest _currentRequest;

        public HuggingFaceTTSService(TTSConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
            _audioCache = new Dictionary<string, AudioClip>();
        }

        public async Task<AudioClip> SynthesizeSpeechAsync(string text, string voiceName = null, string language = null)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text cannot be empty", nameof(text));

            string cacheKey = GetCacheKey(text, voiceName, language);
            
            // 1. Cache Check
            if (_config.enableCaching && _audioCache.TryGetValue(cacheKey, out AudioClip cachedClip))
            {
                if (cachedClip != null) 
                {
                    Debug.Log($"[HuggingFaceTTS] Using cached audio for: {text.Substring(0, Math.Min(20, text.Length))}...");
                    return cachedClip;
                }
                _audioCache.Remove(cacheKey); // Clean up dead reference
            }

            var tcs = new TaskCompletionSource<AudioClip>();
            _coroutineRunner.StartCoroutine(GenerateAudioCoroutine(text, cacheKey, tcs));
            return await tcs.Task;
        }

        private IEnumerator GenerateAudioCoroutine(string text, string cacheKey, TaskCompletionSource<AudioClip> tcs)
        {
            // 1. Get URL dynamically from Config
            // This allows switching models just by changing the URL in the Inspector
            string url = _config.GetFullUrl(); 

            // 2. Build Payload (Heuristic: "text" for Routers/Fal, "inputs" for Standard Inference)
            string jsonBody;
            if (url.Contains("router.huggingface.co") || url.Contains("fal.ai"))
            {
                // Router format (Chatterbox, Kokoro)
                jsonBody = JsonUtility.ToJson(new FalRequest { text = text });
            }
            else
            {
                // Standard Inference API format
                jsonBody = JsonUtility.ToJson(new HFInferenceRequest { inputs = text });
            }

            // 3. Send Request
            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                // Only add Auth if key exists
                if (!string.IsNullOrEmpty(_config.apiKey))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {_config.apiKey}");
                }

                Debug.Log($"[HuggingFaceTTS] Requesting: {url}");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[HuggingFaceTTS] Error: {request.error}\nResponse: {request.downloadHandler.text}");
                    tcs.SetException(new Exception($"TTS Request Failed: {request.error}"));
                    yield break;
                }

                // 4. Handle Different Response Types (The Generic Magic)
                string contentType = request.GetResponseHeader("Content-Type");
                
                if (contentType != null && (contentType.Contains("audio") || contentType.Contains("octet-stream")))
                {
                    // CASE A: Direct Audio (Standard Inference API)
                    // The server returned the raw WAV/MP3 bytes directly.
                    Debug.Log("[HuggingFaceTTS] Received direct audio bytes.");
                    yield return ProcessAudioData(request.downloadHandler.data, cacheKey, tcs);
                }
                else
                {
                    // CASE B: JSON Response (Fal.ai / Routers)
                    // The server returned a JSON with a URL to download the audio.
                    Debug.Log("[HuggingFaceTTS] Received JSON response. Parsing for URL...");
                    string responseText = request.downloadHandler.text;
                    string audioUrl = ParseAudioUrl(responseText);
                    
                    if (!string.IsNullOrEmpty(audioUrl))
                    {
                        yield return DownloadAudioFromUrl(audioUrl, cacheKey, tcs);
                    }
                    else
                    {
                        tcs.SetException(new Exception("Could not find audio URL in JSON response."));
                    }
                }
            }
        }

        // Helper: Downloads audio from a secondary URL (for Router/Fal workflow)
        private IEnumerator DownloadAudioFromUrl(string url, string cacheKey, TaskCompletionSource<AudioClip> tcs)
        {
            using (UnityWebRequest audioReq = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                yield return audioReq.SendWebRequest();
                
                if (audioReq.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(audioReq);
                    FinalizeAudioClip(clip, cacheKey, tcs);
                }
                else
                {
                    tcs.SetException(new Exception($"Failed to download audio file: {audioReq.error}"));
                }
            }
        }

        // Helper: Processes raw bytes into an AudioClip (for Standard Inference workflow)
        private IEnumerator ProcessAudioData(byte[] data, string cacheKey, TaskCompletionSource<AudioClip> tcs)
        {
            // We use a temporary trick: create a temporary valid WAV file in memory or use a specific handler
            // Simpler approach: WavUtility or generic handler. 
            // Since UnityWebRequestMultimedia is tricky with raw bytes, we often re-wrap it or use a helper.
            // For simplicity here, we assume the API sends a valid WAV and use a helper or existing handler logic.
            
            // Note: Converting raw bytes to AudioClip at runtime without a URL is complex in Unity. 
            // To keep it simple and robust, we will save to a temp file and load it, 
            // OR use a third-party WAV parser. 
            // FOR NOW: We will assume most users use the Router (JSON) format. 
            // If you need Raw Byte support, you might need a "WavUtility.ToAudioClip(bytes)" helper.
            
            Debug.LogWarning("[HuggingFaceTTS] Raw byte parsing is complex. Ensure you are using a model that returns a URL or implement a WAV Byte parser.");
            tcs.SetException(new NotImplementedException("Direct byte parsing requires a WAV parser helper. Please use a Router URL model for now."));
            yield break;
        }

        private void FinalizeAudioClip(AudioClip clip, string cacheKey, TaskCompletionSource<AudioClip> tcs)
        {
            if (clip != null)
            {
                clip.name = cacheKey;
                CacheAudioClip(cacheKey, clip);
                tcs.SetResult(clip);
            }
            else
            {
                tcs.SetException(new Exception("Downloaded audio clip was null (decoding failed)."));
            }
        }

        private string ParseAudioUrl(string json)
        {
            // Try Flat format
            var flat = JsonUtility.FromJson<FalResponse>(json);
            if (!string.IsNullOrEmpty(flat.audio_url)) return flat.audio_url;

            // Try Nested format
            var nested = JsonUtility.FromJson<FalResponseNested>(json);
            if (nested.audio != null && !string.IsNullOrEmpty(nested.audio.url)) return nested.audio.url;

            return null;
        }

        private void CacheAudioClip(string key, AudioClip clip)
        {
            if (_audioCache.Count >= _config.maxCacheSize)
            {
                // Remove oldest (simple generic approach)
                var enumerator = _audioCache.Keys.GetEnumerator();
                enumerator.MoveNext();
                string oldestKey = enumerator.Current;
                
                // CRITICAL: Destroy the object to free memory!
                if (_audioCache.TryGetValue(oldestKey, out AudioClip oldClip))
                {
                    UnityEngine.Object.Destroy(oldClip);
                }
                _audioCache.Remove(oldestKey);
            }
            _audioCache[key] = clip;
        }

        private string GetCacheKey(string text, string voice, string language) => $"{text}_{voice}_{language}".GetHashCode().ToString();

        // --- Data Classes ---
        
        [Serializable] private class FalRequest { public string text; }
        [Serializable] private class HFInferenceRequest { public string inputs; }
        [Serializable] private class FalResponse { public string audio_url; }
        [Serializable] private class FalResponseNested { public FalAudioData audio; }
        [Serializable] private class FalAudioData { public string url; }
        
        // Stubs for interface compliance
        public Task<string[]> GetAvailableVoicesAsync() => Task.FromResult(new[] { "default" });
        public Task<bool> IsAvailableAsync() => Task.FromResult(!string.IsNullOrEmpty(_config.apiKey));
        public void CancelSynthesis() { if (_currentRequest != null) _currentRequest.Abort(); }
        
        /// <summary>
        /// SetSpeed is not supported by HuggingFace TTS API.
        /// This is a stub for interface compliance.
        /// </summary>
        public void SetSpeed(float speed)
        {
            Debug.LogWarning("[HuggingFaceTTS] SetSpeed is not supported by HuggingFace TTS provider. Speed parameter ignored.");
        }
    }
}
