using UnityEngine;

namespace LanguageTutor.Data
{
    /// <summary>
    /// Configuration for Speech-to-Text service.
    /// Create via: Assets -> Create -> Language Tutor -> STT Config
    /// </summary>
    [CreateAssetMenu(fileName = "STTConfig", menuName = "Language Tutor/STT Config", order = 3)]
    public class STTConfig : ScriptableObject
    {
        [Header("Provider Selection")]
        [Tooltip("Speech-to-text provider. WhisperLocal runs on device, HuggingFace uses cloud API.")]
        public STTProvider provider = STTProvider.HuggingFace;

        [Header("Authentication (for Cloud Providers)")]
        [Tooltip("API Key for cloud providers (HuggingFace, Azure, Google, AWS). Leave empty for local Whisper.")]
        [TextArea(1, 3)]
        public string apiKey = "";

        [Header("Language Settings")]
        [Tooltip("Default language code for transcription (e.g., 'en', 'de', 'es')")]
        public string defaultLanguage = "en";

        [Header("Recording Settings")]
        [Tooltip("Maximum recording duration in seconds")]
        [Range(5, 120)]
        public int maxRecordingDuration = 30;

        [Tooltip("Microphone sample rate (Hz)")]
        public int sampleRate = 44100;

        [Tooltip("Trim silence threshold (0.0 - 1.0)")]
        [Range(0.0f, 1.0f)]
        public float silenceThreshold = 0.01f;

        [Tooltip("Minimum audio length in samples before processing")]
        [Range(100, 10000)]
        public int minAudioLength = 1000;

        [Header("Processing")]
        [Tooltip("Enable voice activity detection to auto-stop recording")]
        public bool enableVAD = false;

        [Tooltip("Silence duration (seconds) to auto-stop recording")]
        [Range(0.5f, 5.0f)]
        public float vadSilenceDuration = 1.5f;

        [Tooltip("Enable pronunciation assessment with confidence scores")]
        public bool enablePronunciationAssessment = false;

        [Header("Performance")]
        [Tooltip("Request timeout in seconds")]
        [Range(5, 60)]
        public int timeoutSeconds = 30;

        [Tooltip("Number of retry attempts on failure")]
        [Range(0, 5)]
        public int maxRetries = 2;

        [Header("Whisper Model Settings")]
        [Tooltip("For HuggingFace: Model name (e.g., 'openai/whisper-large-v3-turbo'). For Local: model size (tiny, base, small, medium, large)")]
        public string whisperModelName = "openai/whisper-large-v3-turbo";

        [Tooltip("For local Whisper: Model size (tiny, base, small, medium, large). Ignored for cloud providers.")]
        public string whisperModelSize = "base";

        [Tooltip("Enable translation to English (if supported by provider)")]
        public bool whisperTranslateToEnglish = false;
    }

    /// <summary>
    /// Available STT providers.
    /// WhisperLocal: Runs Whisper on device (slow on Quest, fast on PC with GPU)
    /// HuggingFace: Uses HuggingFace Inference API (requires API key, fast on any device)
    /// </summary>
    public enum STTProvider
    {
        [Tooltip("Local on-device Whisper using Whisper.Unity package")]
        WhisperLocal,
        
        [Tooltip("HuggingFace Inference API (Whisper models via cloud)")]
        HuggingFace,
        
        [Tooltip("Azure Speech Services (not yet implemented)")]
        Azure,
        
        [Tooltip("Google Cloud Speech-to-Text (not yet implemented)")]
        Google,
        
        [Tooltip("AWS Transcribe (not yet implemented)")]
        AWS
    }
}
