using UnityEngine;
using Whisper;
using LanguageTutor.Services.LLM;
using LanguageTutor.Services.STT;
using LanguageTutor.Services.TTS;
using LanguageTutor.Actions;
using LanguageTutor.Data;
using LanguageTutor.UI;
using LanguageTutor.Games.Spelling;
using System;
using System.Threading.Tasks;

namespace LanguageTutor.Core
{
    /// <summary>
    /// Main controller for the NPC conversation system.
    /// Orchestrates AudioInput, ConversationPipeline, and NPCView.
    /// </summary>
    public class NPCController : MonoBehaviour
    {
        public static event Action<ConversationGameMode> OnGameModeChanged;
        public static event Action<bool> OnListeningStateChanged;

        [Header("Configuration")]
        [SerializeField] private LanguageTutorConfig config;

        [Header("Components")]
        [SerializeField] private WhisperManager whisperManager;
        [SerializeField] private NPCView npcView;
        [SerializeField] private AvatarAnimationController avatarAnimationController;
        [SerializeField] private PassthroughFrameCapture passthroughFrameCapture;

        // Services
        private ILLMService _llmService;
        private ITTSService _ttsService;
        private ISTTService _sttService;

        // Core Systems
        private AudioInputController _audioInput;
        private ConversationPipeline _conversationPipeline;
        private LLMActionExecutor _actionExecutor;
        private ConversationHistory _conversationHistory;
        private DetectedObjectManager _detectedObjectManager;  // For Word Building mode

        private ILLMAction _currentAction;
        private AudioClip _lastTTSClip;
        private bool _isProcessing;
        private ConversationGameMode _currentGameMode = ConversationGameMode.FreeTalk;

        private const bool DefaultShowSubtitles = true;
        private const bool DefaultAutoPlayTts = true;

        public ConversationGameMode CurrentGameMode => _currentGameMode;

        private void Start()
        {
            InitializeServices();
            InitializeSystems();
            SetupEventListeners();
            SetDefaultAction();

            if (npcView != null) npcView.SetIdleState();

            if (avatarAnimationController != null)
                avatarAnimationController.PlayGreeting();

            Debug.Log("[NPCController] Initialized successfully");
        }

        private void OnDestroy()
        {
            CleanupEventListeners();
        }

        // -------------------------------------------------------------------------
        // SECTION: Game & External Hooks
        // -------------------------------------------------------------------------

        /// <summary>
        /// Allows external systems (like the Spelling Game) to make the NPC speak immediately.
        /// </summary>
        public async void Speak(string text)
        {
            Debug.Log($"[NPCController] Tutor says: {text}");

            if (avatarAnimationController != null)
                avatarAnimationController.SetTalking();

            if (npcView != null)
                npcView.ShowNPCMessage(text);

            if (_ttsService != null)
            {
                var audioClip = await _ttsService.SynthesizeSpeechAsync(text);

                if (audioClip != null && npcView != null)
                {
                    npcView.PlayAudio(audioClip);
                    npcView.SetSpeakingState();
                }
            }
        }

        /// <summary>
        /// Triggers a celebration animation (called by Spelling Game on win).
        /// </summary>
        public void PlaySuccessAnimation()
        {
            Debug.Log("[NPCController] Playing Success Animation");
            if (avatarAnimationController != null)
            {
                avatarAnimationController.PlayGreeting();
            }
        }

        // -------------------------------------------------------------------------
        // SECTION: Core Controller Logic
        // -------------------------------------------------------------------------

        public void SetGameMode(ConversationGameMode mode)
        {
            if (config == null)
                return;

            if (_currentGameMode == ConversationGameMode.WordClouds && mode != ConversationGameMode.WordClouds)
            {
                ClearWordCloudsLetterBoxes();
            }

            config.conversation.gameMode = mode;
            _currentAction = CreateActionForGameMode(mode);
            UpdateModeLabel(mode);
            Debug.Log($"[NPCController] Game mode set to: {mode}");
            LogGameModePrompt(mode);
            StartModeSideEffects(mode);
            _currentGameMode = mode;
            OnGameModeChanged?.Invoke(mode);
        }

        public void ResetConversation()
        {
            if (_conversationPipeline != null) _conversationPipeline.ResetConversation();
            _lastTTSClip = null;
            if (npcView != null)
            {
                npcView.ClearSubtitle();
                npcView.ShowStatusMessage("Conversation reset");
            }
        }

        public ConversationSummary GetConversationSummary()
        {
            return _conversationHistory != null ? _conversationHistory.GetSummary() : new ConversationSummary();
        }

        public void SetTTSSpeed(float speed)
        {
            if (_ttsService != null) _ttsService.SetSpeed(speed);
        }

        public void ReplayLastMessage()
        {
            if (_lastTTSClip != null && npcView != null)
            {
                npcView.PlayAudio(_lastTTSClip);
                npcView.SetSpeakingState();
                if (avatarAnimationController != null) avatarAnimationController.SetTalking();
            }
        }

        public void StopCurrentSpeech()
        {
            if (npcView != null)
            {
                npcView.StopAudio();
                npcView.SetIdleState();
            }

            if (avatarAnimationController != null)
            {
                avatarAnimationController.SetIdle();
            }

            Debug.Log("[NPCController] Speech stopped by user");
        }

        // -------------------------------------------------------------------------
        // SECTION: Initialization & Event Setup
        // -------------------------------------------------------------------------

        private void InitializeServices()
        {
            if (config == null)
            {
                Debug.LogError("[NPCController] Configuration missing!");
                enabled = false;
                return;
            }

            if (STTServiceFactory.RequiresWhisperManager(config.stt.provider) && whisperManager == null)
            {
                Debug.LogError("[NPCController] WhisperManager missing.");
                enabled = false;
                return;
            }

            _llmService = LLMServiceFactory.CreateService(config.llm, this);
            _ttsService = TTSServiceFactory.CreateService(config.tts, this);
            _sttService = STTServiceFactory.CreateService(config.stt, this, whisperManager);
        }

        private void InitializeSystems()
        {
            _conversationHistory = new ConversationHistory();
            _actionExecutor = new LLMActionExecutor(_llmService, config.llm.maxRetries, config.llm.retryDelaySeconds);
            _conversationPipeline = new ConversationPipeline(_sttService, _ttsService, _actionExecutor, _conversationHistory);
            _audioInput = new AudioInputController(config.stt.maxRecordingDuration, config.stt.sampleRate, config.stt.silenceThreshold);

            if (passthroughFrameCapture == null)
                passthroughFrameCapture = FindObjectOfType<PassthroughFrameCapture>(true);

            // Disable Object Detection Visualizer by default (only enable for ObjectTagging mode)
#if MRUK_INSTALLED
            var detectionRecorder = FindObjectOfType<ObjectDetectionListRecorder>();
            if (detectionRecorder != null)
            {
                detectionRecorder.SetVisualizerEnabled(false);
                Debug.Log("[NPCController] ObjectDetectionVisualizer disabled at startup");
            }
#endif

            // Find or create DetectedObjectManager for Word Building mode
            _detectedObjectManager = FindObjectOfType<DetectedObjectManager>();
            if (_detectedObjectManager == null)
            {
                Debug.Log("[NPCController] Creating DetectedObjectManager...");
                var managerGO = new GameObject("DetectedObjectManager");
                _detectedObjectManager = managerGO.AddComponent<DetectedObjectManager>();
            }
            else
            {
                Debug.Log("[NPCController] Found existing DetectedObjectManager");
            }
        }

        private void SetupEventListeners()
        {
            Debug.Log("[NPCController] Setting up event listeners...");

            if (npcView != null && npcView.GetTalkButton() != null)
            {
                npcView.GetTalkButton().onClick.AddListener(OnTalkButtonPressed);
                Debug.Log("[NPCController] Talk Button listener added");
            }
            else
            {
                Debug.LogError("[NPCController] npcView or TalkButton is NULL!");
            }

            if (_audioInput != null)
            {
                _audioInput.OnRecordingStarted += HandleRecordingStarted;
                _audioInput.OnRecordingCompleted += HandleRecordingCompleted;
                _audioInput.OnRecordingError += HandleRecordingError;
                Debug.Log("[NPCController] Audio input listeners added");
            }
            else
            {
                Debug.LogError("[NPCController] _audioInput is NULL!");
            }

            if (_conversationPipeline != null)
            {
                _conversationPipeline.OnStageChanged += HandlePipelineStageChanged;
                _conversationPipeline.OnTranscriptionCompleted += HandleTranscriptionCompleted;
                _conversationPipeline.OnLLMResponseReceived += HandleLLMResponseReceived;
                _conversationPipeline.OnTTSAudioGenerated += HandleTTSAudioGenerated;
                _conversationPipeline.OnPipelineError += HandlePipelineError;
                Debug.Log("[NPCController] Pipeline listeners added");
            }
            else
            {
                Debug.LogError("[NPCController] _conversationPipeline is NULL!");
            }
        }

        private void CleanupEventListeners()
        {
            if (npcView != null && npcView.GetTalkButton() != null)
                npcView.GetTalkButton().onClick.RemoveListener(OnTalkButtonPressed);

            if (_audioInput != null)
            {
                _audioInput.OnRecordingStarted -= HandleRecordingStarted;
                _audioInput.OnRecordingCompleted -= HandleRecordingCompleted;
                _audioInput.OnRecordingError -= HandleRecordingError;
            }

            if (_conversationPipeline != null)
            {
                _conversationPipeline.OnStageChanged -= HandlePipelineStageChanged;
                _conversationPipeline.OnTranscriptionCompleted -= HandleTranscriptionCompleted;
                _conversationPipeline.OnLLMResponseReceived -= HandleLLMResponseReceived;
                _conversationPipeline.OnTTSAudioGenerated -= HandleTTSAudioGenerated;
                _conversationPipeline.OnPipelineError -= HandlePipelineError;
            }
        }

        private void SetDefaultAction()
        {
            if (config != null)
                SetGameMode(config.conversation.gameMode);
        }

        private void OnTalkButtonPressed()
        {
            Debug.Log("[OnTalkButtonPressed] Button pressed!");

            if (_isProcessing)
            {
                Debug.Log("[OnTalkButtonPressed] Still processing, ignoring");
                return;
            }

            if (_audioInput == null)
            {
                Debug.LogError("[OnTalkButtonPressed] _audioInput is NULL");
                return;
            }

            Debug.Log("[OnTalkButtonPressed] Calling ToggleRecording");
            _audioInput.ToggleRecording();
        }

        private void HandleRecordingStarted()
        {
            OnListeningStateChanged?.Invoke(true);
            if (npcView != null) npcView.SetListeningState();
        }

        private async void HandleRecordingCompleted(AudioClip audioClip)
        {
            OnListeningStateChanged?.Invoke(false);
            if (audioClip == null) return;
            _isProcessing = true;

            if (_currentGameMode == ConversationGameMode.ObjectTagging && config != null)
            {
                string languageName = GetLanguageName(config.conversation.language);
                string systemPrompt = BuildGameModeSystemPrompt(_currentGameMode, languageName);
                _currentAction = new ChatAction(systemPrompt);
            }

            var result = await _conversationPipeline.ExecuteAsync(audioClip, _currentAction);

            if (result.Success && result.TTSAudioClip != null && DefaultAutoPlayTts)
            {
                _lastTTSClip = result.TTSAudioClip;
                if (npcView != null)
                {
                    npcView.PlayAudio(result.TTSAudioClip);
                    npcView.SetSpeakingState();
                }
                if (avatarAnimationController != null) avatarAnimationController.SetTalking();
            }
            _isProcessing = false;

            if (!result.Success && npcView != null)
            {
                npcView.SetIdleState();
                if (avatarAnimationController != null) avatarAnimationController.SetIdle();
            }
        }

        private void HandleRecordingError(string error)
        {
            OnListeningStateChanged?.Invoke(false);
            if (npcView != null)
            {
                npcView.ShowErrorMessage(error);
                npcView.SetIdleState();
            }
            _isProcessing = false;
        }

        private void HandlePipelineStageChanged(PipelineStage stage)
        {
            string status = stage switch
            {
                PipelineStage.Transcribing => "Transcribing...",
                PipelineStage.GeneratingResponse => "Thinking...",
                PipelineStage.SynthesizingSpeech => "Generating voice...",
                _ => ""
            };

            if (!string.IsNullOrEmpty(status))
            {
                if (npcView != null) npcView.SetProcessingState(status);
                if (avatarAnimationController != null) avatarAnimationController.SetThinking();
            }
            else if (stage == PipelineStage.Error)
            {
                if (npcView != null) npcView.SetIdleState();
                if (avatarAnimationController != null) avatarAnimationController.SetIdle();
            }
        }

        private void HandleTranscriptionCompleted(string text) { if (DefaultShowSubtitles && npcView != null) npcView.ShowUserMessage(text); }
        private void HandleLLMResponseReceived(string response) { if (DefaultShowSubtitles && npcView != null) npcView.ShowNPCMessage(response); }
        private void HandleTTSAudioGenerated(AudioClip audio) { }
        private void HandlePipelineError(string error) { if (npcView != null) npcView.ShowErrorMessage(error); }

        private void Update()
        {
            if (!_isProcessing && _audioInput != null && !_audioInput.IsRecording && npcView != null && !npcView.IsAudioPlaying())
            {
                npcView.SetIdleState();
                if (avatarAnimationController != null && avatarAnimationController.GetCurrentState() != AnimationState.Idle)
                {
                    avatarAnimationController.SetIdle();
                }
            }
        }

        private ILLMAction CreateActionForGameMode(ConversationGameMode mode)
        {
            string languageName = GetLanguageName(config.conversation.language);
            string systemPrompt = BuildGameModeSystemPrompt(mode, languageName);

            if (mode == ConversationGameMode.FreeTalk)
            {
                if (passthroughFrameCapture == null)
                {
                    Debug.LogWarning("[NPCController] PassthroughFrameCapture not found. FreeTalk will run in text-only mode.");
                    return new ChatAction(systemPrompt);
                }

                return new FreeTalkVisionAction(systemPrompt, passthroughFrameCapture);
            }

            return new ChatAction(systemPrompt);
        }

        private void LogGameModePrompt(ConversationGameMode mode)
        {
            if (config == null)
                return;

            string languageName = GetLanguageName(config.conversation.language);
            string systemPrompt = BuildGameModeSystemPrompt(mode, languageName);
            Debug.Log($"[NPCController] LLM context updated for {mode} ({languageName}):\n{systemPrompt}");
        }

        private string BuildGameModeSystemPrompt(ConversationGameMode mode, string languageName)
        {
            string context;
            string prompt;

            switch (mode)
            {
                case ConversationGameMode.WordClouds:
                    context = config.llm.wordCloudsContext;
                    prompt = config.llm.wordCloudsSystemPrompt;
                    break;
                case ConversationGameMode.ObjectTagging:
                    context = config.llm.objectTaggingContext;
                    prompt = config.llm.objectTaggingSystemPrompt;
                    break;
                case ConversationGameMode.RolePlay:
                    context = config.llm.rolePlayContext;
                    prompt = config.llm.rolePlaySystemPrompt;
                    break;
                case ConversationGameMode.FreeTalk:
                default:
                    context = config.llm.freeTalkContext;
                    prompt = config.llm.freeTalkSystemPrompt;
                    break;
            }

            context = ReplaceLanguageToken(context, languageName);
            prompt = ReplaceLanguageToken(prompt, languageName);

            // For ObjectTagging mode, inject detected objects into the CONTEXT
            if (mode == ConversationGameMode.ObjectTagging && _detectedObjectManager != null)
            {
                var detectedObjects = _detectedObjectManager.GetObjectLabels();
                if (detectedObjects != null && detectedObjects.Count > 0)
                {
                    string objectList = string.Join(", ", detectedObjects);
                    string objectContext = $"\n\nObjects detected in the room: {objectList}. You can see these objects and can describe them or answer questions about them.";
                    context = context + objectContext;
                    Debug.Log($"[NPCController] ObjectTagging context enriched with detected objects: {objectList}");
                }
            }

            if (string.IsNullOrWhiteSpace(context))
                return prompt;

            if (string.IsNullOrWhiteSpace(prompt))
                return context;

            return $"{context}\n\n{prompt}";
        }

        private string ReplaceLanguageToken(string value, string languageName)
        {
            return string.IsNullOrEmpty(value) ? value : value.Replace("{language}", languageName);
        }

        private string GetLanguageName(ConversationLanguage language)
        {
            switch (language)
            {
                case ConversationLanguage.German:
                    return "German";
                default:
                    return "English";
            }
        }

        private void UpdateModeLabel(ConversationGameMode mode)
        {
            if (npcView == null)
                return;

            npcView.SetModeLabel($"Mode: {GetGameModeDisplayName(mode)}");
        }

        private string GetGameModeDisplayName(ConversationGameMode mode)
        {
            switch (mode)
            {
                case ConversationGameMode.WordClouds:
                    return "Word Clouds";
                case ConversationGameMode.ObjectTagging:
                    return "Object Tagging";
                case ConversationGameMode.RolePlay:
                    return "Role Play";
                case ConversationGameMode.FreeTalk:
                default:
                    return "Free Talk";
            }
        }

        private void StartModeSideEffects(ConversationGameMode mode)
        {
            // Object Detection Visualizer: ONLY enabled for ObjectTagging mode
            // Disabled for: FreeTalk, WordClouds (Word Building), RolePlay
#if MRUK_INSTALLED
            var detectionRecorder = FindObjectOfType<ObjectDetectionListRecorder>();
            if (detectionRecorder != null)
            {
                bool shouldEnable = (mode == ConversationGameMode.ObjectTagging);
                detectionRecorder.SetVisualizerEnabled(shouldEnable);
                
                if (shouldEnable)
                    Debug.Log($"[NPCController] Visualizer ENABLED for ObjectTagging mode");
                else
                    Debug.Log($"[NPCController] Visualizer DISABLED - Mode is {mode} (only enabled for ObjectTagging)");
            }
#endif

            // Word Building mode specific setup
            if (mode == ConversationGameMode.WordClouds)
            {
                ClearWordCloudsLetterBoxes();
                StartWordCloudsLetterBoxes();
            }
        }

        private void StartWordCloudsLetterBoxes()
        {
            Debug.Log("[NPCController] Starting Word Clouds letterboxes...");

            // Get detected object names for Word Building mode
            var detectedWords = _detectedObjectManager?.GetObjectLabels();

            if (detectedWords != null && detectedWords.Count > 0)
            {
                Debug.Log($"[NPCController] Word Building mode - using {detectedWords.Count} detected objects: {string.Join(", ", detectedWords)}");
                var gameAction = new SpellingGameAction(detectedWords);
                _ = gameAction.ExecuteAsync(_llmService, new LLMActionContext());
            }
            else
            {
                Debug.LogWarning("[NPCController] No detected objects found. Using default word list.");
                var gameAction = new SpellingGameAction();  // Falls back to "CAT"
                _ = gameAction.ExecuteAsync(_llmService, new LLMActionContext());
            }
        }

        private void ClearWordCloudsLetterBoxes()
        {
            var slots = FindObjectsOfType<LetterSlot>(true);
            var blocks = FindObjectsOfType<LetterBlock>(true);

            int destroyed = 0;
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                    destroyed++;
                }
            }

            foreach (var block in blocks)
            {
                if (block != null)
                {
                    Destroy(block.gameObject);
                    destroyed++;
                }
            }

            if (destroyed > 0)
            {
                Debug.Log($"[NPCController] Cleared Word Clouds letterboxes: {destroyed} objects destroyed.");
            }
        }
    }
}
