using UnityEngine;

namespace LanguageTutor.Data
{
    /// <summary>
    /// TTS provider types.
    /// </summary>
    public enum TTSProvider
    {
        AllTalk
    }

    /// <summary>
    /// Configuration for Text-to-Speech service.
    /// Create via: Assets -> Create -> Language Tutor -> TTS Config
    /// </summary>
    [CreateAssetMenu(fileName = "TTSConfig", menuName = "Language Tutor/TTS Config", order = 2)]
    public class TTSConfig : ScriptableObject
    {
        [Header("Provider Selection")]
        [Tooltip("Choose the TTS provider to use (AllTalk only)")]
        public TTSProvider provider = TTSProvider.AllTalk;

        [Header("═══════════ LOCAL SERVICES (AllTalk) ═══════════")]
        [Tooltip("Base URL for local AllTalk service")]
        public string allTalkServiceUrl = "http://127.0.0.1:7851";

        [Tooltip("API endpoint path for AllTalk")]
        public string allTalkEndpointPath = "/api/tts-generate";

        [Tooltip("Voice file for AllTalk (e.g., male_01.wav)")]
        public string allTalkVoice = "male_01.wav";

        [Header("═══════════ API SERVICES ═══════════")]
        [Space(10)]
        [Header("HuggingFace API")]
        [Tooltip("HuggingFace TTS API URL (legacy, unused when AllTalk is selected)")]
        public string huggingFaceApiUrl = "https://router.huggingface.co/fal-ai/fal-ai/kokoro/american-english";

        [Header("Authentication")]
        [Tooltip("API Key for HuggingFace API (legacy)")]
        [TextArea(1, 3)]
        public string apiKey = "";

        // Backward compatibility properties
        public string serviceUrl
        {
            get
            {
                return allTalkServiceUrl;
            }
        }

        public string endpointPath
        {
            get
            {
                return allTalkEndpointPath;
            }
        }

        public string defaultVoice
        {
            get
            {
                return allTalkVoice;
            }
        }

        [Header("Voice Settings")]

        [Tooltip("Default language code (e.g., 'en', 'de', 'es', 'fr')")]
        public string defaultLanguage = "en";

        [Tooltip("Speech rate/speed (0.5 = slow, 1.0 = normal, 2.0 = fast)")]
        [Range(0.5f, 2.0f)]
        public float speechRate = 1.0f;

        [Tooltip("Voice pitch adjustment")]
        [Range(0.5f, 2.0f)]
        public float pitch = 1.0f;

        [Header("Audio Quality")]
        [Tooltip("Sample rate for generated audio (Hz)")]
        public int sampleRate = 22050;

        [Tooltip("Audio output format (wav, mp3, ogg)")]
        public AudioFormat outputFormat = AudioFormat.WAV;

        [Header("Performance")]
        [Tooltip("Request timeout in seconds")]
        [Range(5, 60)]
        public int timeoutSeconds = 20;

        [Tooltip("Number of retry attempts on failure")]
        [Range(0, 5)]
        public int maxRetries = 2;

        [Tooltip("Cache generated audio clips to avoid re-generation")]
        public bool enableCaching = true;

        [Tooltip("Maximum number of cached audio clips")]
        [Range(10, 100)]
        public int maxCacheSize = 50;

        [Header("Advanced")]
        [Tooltip("Split long texts into smaller chunks for faster generation")]
        public bool enableTextChunking = true;

        [Tooltip("Maximum characters per chunk")]
        [Range(50, 500)]
        public int maxChunkLength = 200;

        /// <summary>
        /// Get the full service URL (base + endpoint)
        /// </summary>
        public string GetFullUrl()
        {
            return serviceUrl.TrimEnd('/') + "/" + endpointPath.TrimStart('/');
        }
    }

    public enum AudioFormat
    {
        WAV,
        MP3,
        OGG
    }
}
