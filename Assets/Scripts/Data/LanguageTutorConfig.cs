using System;
using UnityEngine;

namespace LanguageTutor.Data
{
    /// <summary>
    /// Unified configuration for LLM, STT, TTS, and conversation behavior.
    /// Create via: Assets -> Create -> Language Tutor -> Language Tutor Config
    /// </summary>
    [CreateAssetMenu(fileName = "LanguageTutorConfig", menuName = "Language Tutor/Language Tutor Config", order = 1)]
    public class LanguageTutorConfig : ScriptableObject
    {
        [Header("LLM")]
        public LLMSettings llm = new LLMSettings();

        [Header("STT")]
        public STTSettings stt = new STTSettings();

        [Header("TTS")]
        public TTSSettings tts = new TTSSettings();

        [Header("Conversation")]
        public ConversationSettings conversation = new ConversationSettings();
    }

    [Serializable]
    public class LLMSettings
    {
        [Header("Provider Selection")]
        [Tooltip("Choose the LLM provider to use (HuggingFace only)")]
        public LLMProvider provider = LLMProvider.HuggingFace;

        [Header("Service Configuration")]
        [Tooltip("Base URL for legacy services (unused for HuggingFace router)")]
        public string serviceUrl = "http://127.0.0.1:11434";

        [Tooltip("Endpoint path for legacy services (unused for HuggingFace router)")]
        public string endpointPath = "/api/generate";

        [Tooltip("Model name to use (e.g., google/gemma-3-27b-it)")]
        public string modelName = "google/gemma-3-27b-it";

        [Header("Authentication")]
        [Tooltip("API Key for cloud providers (HuggingFace).")]
        [TextArea(1, 3)]
        public string apiKey = "";

        [Header("Request Settings")]
        [Tooltip("Maximum number of tokens in the response")]
        [Range(50, 4096)]
        public int maxTokens = 512;

        [Tooltip("Temperature for response randomness (0.0 = deterministic, 1.0 = creative)")]
        [Range(0.0f, 2.0f)]
        public float temperature = 0.7f;

        [Tooltip("Enable streaming responses (if supported by provider)")]
        public bool enableStreaming = false;

        [Header("Retry & Timeout")]
        [Tooltip("Request timeout in seconds")]
        [Range(5, 120)]
        public int timeoutSeconds = 30;

        [Tooltip("Number of retry attempts on failure")]
        [Range(0, 5)]
        public int maxRetries = 2;

        [Tooltip("Delay between retries in seconds")]
        [Range(1, 10)]
        public float retryDelaySeconds = 2.0f;

        [Header("System Prompts")]
        [Tooltip("Default system prompt for general conversation (fallback)")]
        [TextArea(3, 6)]
        public string defaultSystemPrompt = "You are a helpful language learning assistant. Provide clear, concise responses that help the user practice the language. Keep responses to 1-2 sentences unless asked for more detail.";

        [Header("Game Mode Prompts")]
        [Tooltip("Free Talk LLM context (use {language} for current language)")]
        [TextArea(3, 6)]
        public string freeTalkContext = "You are a friendly language tutor. You can speak multiple languages.";

        [Tooltip("Free Talk system prompt (use {language} for current language)")]
        [TextArea(3, 6)]
        public string freeTalkSystemPrompt = "Have a natural, open conversation in any language the users ask you about. Keep responses concise and encouraging.";

        [Tooltip("Word Clouds LLM context (use {language} for current language)")]
        [TextArea(3, 6)]
        public string wordCloudsContext = "You help generate word clusters for language learning.";

        [Tooltip("Word Clouds system prompt (use {language} for current language)")]
        [TextArea(3, 6)]
        public string wordCloudsSystemPrompt = "Suggest short words with the purpose of ordering the characters of it and encourage the user to solve it.";

        [Tooltip("Object Tagging LLM context (use {language} for current language)")]
        [TextArea(3, 6)]
        public string objectTaggingContext = "You assist with object labeling for language learning.";

        [Tooltip("Object Tagging system prompt (use {language} for current language)")]
        [TextArea(3, 6)]
        public string objectTaggingSystemPrompt = "Label objects you see with short, clear nouns. Take into consideration special cases. For German words for example, include the correct article.";

        [Tooltip("Role Play LLM context (use {language} for current language)")]
        [TextArea(3, 6)]
        public string rolePlayContext = "You are role-playing with the user in the context of helping him practice real world scenarios in a certain language.";

        [Tooltip("Role Play system prompt (use {language} for current language)")]
        [TextArea(3, 6)]
        public string rolePlaySystemPrompt = "Stay in character and drive a role-play conversation. Keep your answers short and interactive for the user.";

        /// <summary>
        /// Get the full service URL (base + endpoint)
        /// </summary>
        public string GetFullUrl()
        {
            return serviceUrl.TrimEnd('/') + "/" + endpointPath.TrimStart('/');
        }
    }

    [Serializable]
    public class STTSettings
    {
        [Header("Provider Selection")]
        [Tooltip("Speech-to-text provider (HuggingFace only)")]
        public STTProvider provider = STTProvider.HuggingFace;

        [Header("Authentication (for Cloud Providers)")]
        [Tooltip("API Key for HuggingFace STT (HF_TOKEN)")]
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



    [Serializable]
    public class TTSSettings
    {

        public enum TTSProvider
        {
            ElevenLabs,
            AllTalk
        }

        [Header("Provider Selection")]
        [Tooltip("Choose the TTS provider to use (ElevenLabs or AllTalk)")]
        public TTSProvider provider = TTSProvider.ElevenLabs;

        [Header("AllTalk Settings")]
        [Tooltip("AllTalk server URL (e.g., http://127.0.0.1:7851)")]
        public string allTalkServerUrl = "http://127.0.0.1:7851";

        [Tooltip("AllTalk API endpoint path")]
        public string allTalkEndpointPath = "/api/tts-generate";

        [Tooltip("AllTalk voice name (e.g., 'female_01', 'male_01')")]
        public string allTalkVoiceName = "female_01";

        [Header("ElevenLabs Settings")]
        [Tooltip("Voice ID for ElevenLabs, Mila")]
        public string elevenLabsVoiceId = "dCnu06FiOZma2KVNUoPZ";

        [Tooltip("Audio output format (e.g., mp3_44100_128, mp3_44100_192, pcm_16000, pcm_22050, pcm_24000, pcm_44100)")]
        public string outputFormat = "mp3_44100_128";

        [Tooltip("Model ID for ElevenLabs (e.g., eleven_multilingual_v2, eleven_monolingual_v1, eleven_turbo_v2)")]
        public string modelId = "eleven_multilingual_v2";

        [Header("Authentication")]
        [Tooltip("API Key for ElevenLabs API")]
        [TextArea(1, 3)]
        public string apiKey = "";

        [Tooltip("Speech rate/speed (0.5 = slow, 1.0 = normal, 2.0 = fast)")]
        [Range(0.5f, 2.0f)]
        public float speechRate = 1.0f;

        [Header("Voice Settings")]
        [Tooltip("Default language code (e.g., 'en', 'de', 'es', 'fr')")]
        public string defaultLanguage = "en";

        [Header("Performance")]
        [Tooltip("Request timeout in seconds")]
        [Range(5, 60)]
        public int timeoutSeconds = 20;

        [Tooltip("Cache generated audio clips to avoid re-generation")]
        public bool enableCaching = true;

        [Tooltip("Maximum number of cached audio clips")]
        [Range(10, 100)]
        public int maxCacheSize = 50;

        public string ServiceUrl
        {
            get
            {
                switch (provider)
                {
                    case TTSProvider.AllTalk:
                        return allTalkServerUrl;
                    case TTSProvider.ElevenLabs:
                    default:
                        return "https://api.elevenlabs.io/v1";
                }
            }
        }

        public string EndpointPath
        {
            get
            {
                switch (provider)
                {
                    case TTSProvider.AllTalk:
                        return allTalkEndpointPath;
                    case TTSProvider.ElevenLabs:
                    default:
                        return $"/text-to-speech/{elevenLabsVoiceId}";
                }
            }
        }

        public string DefaultVoice
        {
            get
            {
                switch (provider)
                {
                    case TTSProvider.AllTalk:
                        return allTalkVoiceName;
                    case TTSProvider.ElevenLabs:
                    default:
                        return elevenLabsVoiceId;
                }
            }
        }

        /// <summary>
        /// Get the full service URL (base + endpoint)
        /// </summary>
        public string GetFullUrl()
        {
            return ServiceUrl.TrimEnd('/') + "/" + EndpointPath.TrimStart('/');
        }
    }

    [Serializable]
    public class ConversationSettings
    {
        [Header("Language")]
        [Tooltip("Language used for the conversation")]
        public ConversationLanguage language = ConversationLanguage.English;

        [Header("Game Mode")]
        [Tooltip("Active game mode for the conversation")]
        public ConversationGameMode gameMode = ConversationGameMode.FreeTalk;
    }
}