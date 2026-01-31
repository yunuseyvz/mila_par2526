using UnityEngine;
using Whisper;
using LanguageTutor.Services.LLM;
using LanguageTutor.Services.STT;
using LanguageTutor.Services.TTS;
using LanguageTutor.Actions;
using LanguageTutor.Data;
using LanguageTutor.UI;

namespace LanguageTutor.Core
{
    /// <summary>
    /// Main controller for NPC conversation system.
    /// Orchestrates AudioInput, ConversationPipeline, and NPCView using dependency injection.
    /// This is the entry point that ties together all the refactored components.
    /// </summary>
    public class NPCController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private LLMConfig llmConfig;
        [SerializeField] private TTSConfig ttsConfig;
        [SerializeField] private STTConfig sttConfig;
        [SerializeField] private ConversationConfig conversationConfig;

        [Header("Components")]
        [SerializeField] private WhisperManager whisperManager;
        [SerializeField] private NPCView npcView;
        [SerializeField] private AvatarAnimationController avatarAnimationController;

        [Header("Action Mode")]
        [SerializeField] private ActionMode defaultActionMode = ActionMode.Chat;

        // Services
        private ILLMService _llmService;
        private ITTSService _ttsService;
        private ISTTService _sttService;

        // Core Systems
        private AudioInputController _audioInput;
        private ConversationPipeline _conversationPipeline;
        private LLMActionExecutor _actionExecutor;
        private ConversationHistory _conversationHistory;

        // Current Action
        private ILLMAction _currentAction;

        // Last TTS Audio for Replay
        private AudioClip _lastTTSClip;

        // State
        private bool _isProcessing;

        private void Start()
        {
            InitializeServices();
            InitializeSystems();
            SetupEventListeners();
            SetDefaultAction();

            npcView.SetIdleState();
            // Play greeting animation when game starts
            if (avatarAnimationController != null)
                avatarAnimationController.PlayGreeting();
            Debug.Log("[NPCController] Initialized successfully");
        }

        private void OnDestroy()
        {
            CleanupEventListeners();
        }

        /// <summary>
        /// Initialize all services with their configurations.
        /// </summary>
        private void InitializeServices()
        {
            // Validate configurations
            if (llmConfig == null || ttsConfig == null || sttConfig == null || conversationConfig == null)
            {
                Debug.LogError("[NPCController] One or more configuration ScriptableObjects are missing! Create them via Assets -> Create -> Language Tutor");
                enabled = false;
                return;
            }

            // WhisperManager is only required for local Whisper provider
            if (STTServiceFactory.RequiresWhisperManager(sttConfig.provider) && whisperManager == null)
            {
                Debug.LogError("[NPCController] WhisperManager is required for WhisperLocal provider! Either assign it or switch to HuggingFace provider in STTConfig.");
                enabled = false;
                return;
            }

            // Initialize services
            _llmService = LLMServiceFactory.CreateService(llmConfig, this);
            _ttsService = TTSServiceFactory.CreateService(ttsConfig, this);
            _sttService = STTServiceFactory.CreateService(sttConfig, this, whisperManager);

            Debug.Log($"[NPCController] Services initialized - LLM: {_llmService.GetModelName()}, TTS: {ttsConfig.provider}, STT: {sttConfig.provider}");
        }

        /// <summary>
        /// Initialize core systems using the services.
        /// </summary>
        private void InitializeSystems()
        {
            // Create conversation history
            _conversationHistory = new ConversationHistory(
                conversationConfig.maxHistoryLength,
                conversationConfig.autoSummarizeHistory
            );

            // Create action executor
            _actionExecutor = new LLMActionExecutor(
                _llmService,
                llmConfig.maxRetries,
                llmConfig.retryDelaySeconds
            );

            // Create conversation pipeline
            _conversationPipeline = new ConversationPipeline(
                _sttService,
                _ttsService,
                _actionExecutor,
                _conversationHistory
            );

            // Create audio input controller
            _audioInput = new AudioInputController(
                sttConfig.maxRecordingDuration,
                sttConfig.sampleRate,
                sttConfig.silenceThreshold
            );

            Debug.Log("[NPCController] Core systems initialized");
        }

        /// <summary>
        /// Setup event listeners for all components.
        /// </summary>
        private void SetupEventListeners()
        {
            // Button click
            if (npcView != null && npcView.GetTalkButton() != null)
            {
                npcView.GetTalkButton().onClick.AddListener(OnTalkButtonPressed);
            }

            // Audio input events
            _audioInput.OnRecordingStarted += HandleRecordingStarted;
            _audioInput.OnRecordingCompleted += HandleRecordingCompleted;
            _audioInput.OnRecordingError += HandleRecordingError;

            // Pipeline events
            _conversationPipeline.OnStageChanged += HandlePipelineStageChanged;
            _conversationPipeline.OnTranscriptionCompleted += HandleTranscriptionCompleted;
            _conversationPipeline.OnLLMResponseReceived += HandleLLMResponseReceived;
            _conversationPipeline.OnTTSAudioGenerated += HandleTTSAudioGenerated;
            _conversationPipeline.OnPipelineError += HandlePipelineError;
        }

        /// <summary>
        /// Cleanup event listeners.
        /// </summary>
        private void CleanupEventListeners()
        {
            if (npcView != null && npcView.GetTalkButton() != null)
            {
                npcView.GetTalkButton().onClick.RemoveListener(OnTalkButtonPressed);
            }

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

        /// <summary>
        /// Set the default action based on configuration.
        /// </summary>
        private void SetDefaultAction()
        {
            SetActionMode(defaultActionMode);
        }

        /// <summary>
        /// Handle talk button press - toggles recording.
        /// </summary>
        private void OnTalkButtonPressed()
        {
            if (_isProcessing)
            {
                Debug.Log("[NPCController] Already processing, ignoring button press");
                return;
            }

            _audioInput.ToggleRecording();
        }

        #region Audio Input Event Handlers

        private void HandleRecordingStarted()
        {
            npcView.SetListeningState();
        }

        private async void HandleRecordingCompleted(AudioClip audioClip)
        {
            Debug.Log("[NPCController] Recording completed, starting processing...");
            
            if (audioClip == null)
            {
                Debug.LogError("[NPCController] AudioClip is null!");
                npcView.ShowErrorMessage("Failed to record audio");
                npcView.SetIdleState();
                return;
            }

            Debug.Log($"[NPCController] AudioClip received - Length: {audioClip.length}s, Samples: {audioClip.samples}");

            _isProcessing = true;

            // Execute the conversation pipeline
            Debug.Log($"[NPCController] Executing pipeline with action: {_currentAction?.GetType().Name}");
            var result = await _conversationPipeline.ExecuteAsync(audioClip, _currentAction);

            Debug.Log($"[NPCController] Pipeline completed - Success: {result.Success}");
            
            if (result.Success)
            {
                Debug.Log($"[NPCController] Transcription: {result.TranscribedText}");
                Debug.Log($"[NPCController] LLM Response: {result.LLMResponse}");
                Debug.Log($"[NPCController] TTS Clip present: {result.TTSAudioClip != null}");
                
                if (result.TTSAudioClip != null && conversationConfig.autoPlayTTS)
                {
                    Debug.Log("[NPCController] Playing TTS audio");
                    _lastTTSClip = result.TTSAudioClip; // Store for replay
                    npcView.PlayAudio(result.TTSAudioClip);
                    npcView.SetSpeakingState();
                    
                    // Trigger talking animation when TTS begins
                    if (avatarAnimationController != null)
                        avatarAnimationController.SetTalking();
                }
            }
            else
            {
                Debug.LogError($"[NPCController] Pipeline failed: {result.ErrorMessage}");
            }

            _isProcessing = false;
            
            if (!result.Success)
            {
                npcView.SetIdleState();
                if (avatarAnimationController != null)
                    avatarAnimationController.SetIdle();
            }
        }

        private void HandleRecordingError(string error)
        {
            npcView.ShowErrorMessage(error);
            npcView.SetIdleState();
            if (avatarAnimationController != null)
                avatarAnimationController.SetIdle();
            _isProcessing = false;
        }

        #endregion

        #region Pipeline Event Handlers

        private void HandlePipelineStageChanged(PipelineStage stage)
        {
            switch (stage)
            {
                case PipelineStage.Transcribing:
                    npcView.SetProcessingState("Transcribing...");
                    if (avatarAnimationController != null)
                        avatarAnimationController.SetThinking();
                    break;
                case PipelineStage.GeneratingResponse:
                    npcView.SetProcessingState("Thinking...");
                    if (avatarAnimationController != null)
                        avatarAnimationController.SetThinking();
                    break;
                case PipelineStage.SynthesizingSpeech:
                    npcView.SetProcessingState("Generating voice...");
                    if (avatarAnimationController != null)
                        avatarAnimationController.SetThinking();
                    break;
                case PipelineStage.Complete:
                    // Will be handled when audio finishes playing
                    break;
                case PipelineStage.Error:
                    npcView.SetIdleState();
                    if (avatarAnimationController != null)
                        avatarAnimationController.SetIdle();
                    break;
            }
        }

        private void HandleTranscriptionCompleted(string text)
        {
            if (conversationConfig.showSubtitles)
            {
                npcView.ShowUserMessage(text);
            }
        }

        private void HandleLLMResponseReceived(string response)
        {
            if (conversationConfig.showSubtitles)
            {
                npcView.ShowNPCMessage(response);
            }
        }

        private void HandleTTSAudioGenerated(AudioClip audio)
        {
            // Audio will be played automatically if autoPlayTTS is enabled
            Debug.Log($"[NPCController] TTS audio generated: {audio.length:F2}s");
        }

        private void HandlePipelineError(string error)
        {
            npcView.ShowErrorMessage(error);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Set the current action mode for conversation.
        /// </summary>
        public void SetActionMode(ActionMode mode)
        {
            defaultActionMode = mode;

            switch (mode)
            {
                case ActionMode.Chat:
                    _currentAction = new ChatAction(llmConfig.defaultSystemPrompt);
                    break;
                case ActionMode.GrammarCheck:
                    _currentAction = new GrammarCheckAction(conversationConfig.targetLanguage, llmConfig.grammarCorrectionPrompt);
                    break;
                case ActionMode.VocabularyTeach:
                    _currentAction = new VocabularyTeachAction(conversationConfig.targetLanguage, llmConfig.vocabularyTeachingPrompt);
                    break;
                case ActionMode.ConversationPractice:
                    _currentAction = new ConversationPracticeAction("casual conversation", llmConfig.conversationPracticePrompt);
                    break;
            }

            Debug.Log($"[NPCController] Action mode set to: {mode}");
        }

        /// <summary>
        /// Reset conversation history.
        /// </summary>
        public void ResetConversation()
        {
            _conversationPipeline.ResetConversation();
            _lastTTSClip = null;
            npcView.ClearSubtitle();
            npcView.ShowStatusMessage("Conversation reset");
        }

        /// <summary>
        /// Get conversation summary.
        /// </summary>
        public ConversationSummary GetConversationSummary()
        {
            return _conversationHistory.GetSummary();
        }

        public void SetTTSSpeed(float speed)
        {
            if (_ttsService != null)
            {
                _ttsService.SetSpeed(speed);
            }
        }

        /// <summary>
        /// Replay the last generated TTS audio.
        /// </summary>
        public void ReplayLastMessage()
        {
            if (_lastTTSClip != null)
            {
                Debug.Log("[NPCController] Replaying last TTS audio");
                npcView.PlayAudio(_lastTTSClip);
                npcView.SetSpeakingState();
                
                if (avatarAnimationController != null)
                    avatarAnimationController.SetTalking();
            }
            else
            {
                Debug.LogWarning("[NPCController] No last TTS clip to replay");
                npcView.ShowStatusMessage("No audio to replay");
            }
        }

        #endregion

        private void Update()
        {
            // If audio finished playing, return to idle state
            if (!_isProcessing && !_audioInput.IsRecording && npcView != null && !npcView.IsAudioPlaying())
            {
                npcView.SetIdleState();
                
                // Return avatar to idle animation
                if (avatarAnimationController != null && avatarAnimationController.GetCurrentState() != AnimationState.Idle)
                {
                    avatarAnimationController.SetIdle();
                }
            }
        }
    }

    /// <summary>
    /// Available action modes for NPC conversation.
    /// </summary>
    public enum ActionMode
    {
        Chat,
        GrammarCheck,
        VocabularyTeach,
        ConversationPractice
    }
}
