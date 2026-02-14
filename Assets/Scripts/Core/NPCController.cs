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
        private ObjectQuizManager _objectQuizManager;           // For Object Quiz mode

        private ILLMAction _currentAction;
        private AudioClip _lastTTSClip;
        private bool _isProcessing;
        private float _processingStartedRealtime = -1f;
        private bool _isWaitingForObjectTaggingObjects;
        private ConversationGameMode _currentGameMode = ConversationGameMode.FreeTalk;
        private Coroutine _objectTaggingRetryRoutine;
        private PipelineStage _lastPipelineStage = PipelineStage.Idle;

        [Header("Object Tagging")]
        [SerializeField] private float objectTaggingRetryIntervalSeconds = 5f;

        private const bool DefaultShowSubtitles = true;
        private const bool DefaultAutoPlayTts = true;
        private const float ProcessingSafetyBufferSeconds = 10f;
        private const float MinimumPipelineTimeoutSeconds = 20f;

        public ConversationGameMode CurrentGameMode => _currentGameMode;

        private void Start()
        {
            InitializeServices();
            InitializeSystems();
            SetupEventListeners();
            SetDefaultAction();
            UpdateTtsProviderStatusUi();

            if (npcView != null) npcView.SetIdleState();

            if (avatarAnimationController != null)
                avatarAnimationController.PlayGreeting();

            Debug.Log("[NPCController] Initialized successfully");
        }

        private void OnDestroy()
        {
            StopObjectTaggingRetryLoop();
            CleanupEventListeners();
        }

        // -------------------------------------------------------------------------
        // SECTION: Game & External Hooks
        // -------------------------------------------------------------------------

        // -------------------------------------------------------------------------
        // SECTION: Game & External Hooks
        // -------------------------------------------------------------------------

        /// <summary>
        /// Allows external systems (like the Spelling Game) to make the NPC speak immediately.
        /// </summary>
        public async void Speak(string text)
        {
            Debug.Log($"[NPCController] Tutor says: {text}");

            // Show Text Bubble immediately
            if (npcView != null)
                npcView.ShowNPCMessage(text);

            // Check for positive keywords to trigger clapping (Same logic as LLM response)
            if (!string.IsNullOrEmpty(text))
            {
                string lowerText = text.ToLowerInvariant();
                if (lowerText.Contains("good") ||
                    lowerText.Contains("great") ||
                    lowerText.Contains("perfect") ||
                    lowerText.Contains("well done"))
                {
                    if (avatarAnimationController != null)
                    {
                        avatarAnimationController.PlayClapping();
                    }
                }
            }

            if (_ttsService != null)
            {
                try
                {
                    var audioClip = await _ttsService.SynthesizeSpeechAsync(text);
                    if (audioClip != null)
                    {
                        PlayAudioWithEarlyStop(audioClip);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NPCController] Speak TTS failed: {ex.Message}");
                    if (npcView != null)
                    {
                        npcView.ShowTtsError(ex.Message);
                    }
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

        public void ReinitializeConversationPipeline()
        {
            Debug.Log("[NPCController] Reinitializing conversation pipeline...");

            StopObjectTaggingRetryLoop();
            CancelOngoingOperations();

            if (_audioInput != null && _audioInput.IsRecording)
            {
                _audioInput.CancelRecording();
            }

            if (npcView != null)
            {
                npcView.StopAudio();
            }

            CleanupEventListeners();
            InitializeServices();

            if (_llmService == null || _ttsService == null || _sttService == null)
            {
                Debug.LogError("[NPCController] Reinitialization failed: one or more services could not be created.");
                if (npcView != null)
                {
                    npcView.ShowErrorMessage("Could not reinitialize conversation services.");
                    npcView.SetIdleState();
                }
                EndProcessing();
                OnListeningStateChanged?.Invoke(false);
                return;
            }

            InitializeSystems();
            SetupEventListeners();

            _lastTTSClip = null;
            EndProcessing();
            OnListeningStateChanged?.Invoke(false);

            if (config != null)
            {
                SetGameMode(ConversationGameMode.FreeTalk);
            }

            if (npcView != null)
            {
                npcView.ClearSubtitle();
                npcView.SetIdleState();
                npcView.ShowStatusMessage("Conversation pipeline reinitialized");
            }

            UpdateTtsProviderStatusUi();

            if (avatarAnimationController != null)
            {
                avatarAnimationController.SetIdle();
            }

            Debug.Log("[NPCController] Conversation pipeline reinitialized successfully");
        }

        public ConversationSummary GetConversationSummary()
        {
            return _conversationHistory != null ? _conversationHistory.GetSummary() : new ConversationSummary();
        }

        public void SetTTSSpeed(float speed)
        {
            if (_ttsService != null) _ttsService.SetSpeed(speed);
        }

        public void ToggleTTSProvider()
        {
            if (config == null)
            {
                Debug.LogError("[NPCController] Cannot toggle TTS provider: config is missing.");
                return;
            }

            var nextProvider = config.tts.provider == TTSSettings.TTSProvider.ElevenLabs
                ? TTSSettings.TTSProvider.AllTalk
                : TTSSettings.TTSProvider.ElevenLabs;

            config.tts.provider = nextProvider;
            Debug.Log($"[NPCController] TTS provider switched to: {nextProvider}");

            ReinitializeConversationPipeline();

            if (npcView != null)
            {
                npcView.ShowStatusMessage($"TTS switched to {GetTtsProviderLabel(nextProvider)}");
            }

            UpdateTtsProviderStatusUi();
        }

        public void ReplayLastMessage()
        {
            if (_lastTTSClip != null)
            {
                PlayAudioWithEarlyStop(_lastTTSClip);
            }
        }

        private Coroutine _talkingRoutine;

        private void PlayAudioWithEarlyStop(AudioClip clip)
        {
            if (clip == null) return;

            // 1. Play Audio & UI
            if (npcView != null)
            {
                npcView.PlayAudio(clip);
                npcView.SetSpeakingState();
            }

            // 2. Start Animation & Coroutine
            if (avatarAnimationController != null)
            {
                avatarAnimationController.SetTalking();

                if (_talkingRoutine != null) StopCoroutine(_talkingRoutine);

                // Stop 0.5s early, but ensure at least 0.1s duration
                float duration = Mathf.Max(0.1f, clip.length - 0.5f);
                _talkingRoutine = StartCoroutine(StopTalkingAfterDelay(duration));
            }
        }

        private System.Collections.IEnumerator StopTalkingAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (avatarAnimationController != null)
            {
                avatarAnimationController.SetIdle();
            }
            _talkingRoutine = null;
        }

        private async Task WaitForSpeechToFinishAsync(int maxStartWaitMs = 5000, int maxTotalWaitMs = 20000)
        {
            if (npcView == null)
                return;

            int waitedToStartMs = 0;
            while (!npcView.IsAudioPlaying() && waitedToStartMs < maxStartWaitMs)
            {
                await Task.Delay(100);
                waitedToStartMs += 100;
            }

            int waitedTotalMs = 0;
            while (npcView.IsAudioPlaying() && waitedTotalMs < maxTotalWaitMs)
            {
                await Task.Delay(100);
                waitedTotalMs += 100;
            }
        }

        public void StopCurrentSpeech()
        {
            if (npcView != null)
            {
                npcView.StopAudio();
                npcView.SetIdleState();
            }

            _ttsService?.CancelSynthesis();

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
                detectionRecorder.SetDetectionEnabled(false);
                detectionRecorder.SetVisualizerEnabled(false);
                Debug.Log("[NPCController] Object detection disabled at startup");
            }

            var objectQuizHighlighter = FindObjectOfType<ObjectQuizHighlighter>();
            if (objectQuizHighlighter != null)
            {
                objectQuizHighlighter.SetDetectionEnabled(false);
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

            // Find or create ObjectQuizManager for Object Quiz mode
            _objectQuizManager = FindObjectOfType<ObjectQuizManager>();
            if (_objectQuizManager == null)
            {
                Debug.Log("[NPCController] Creating ObjectQuizManager...");
                var quizGO = new GameObject("ObjectQuizManager");
                _objectQuizManager = quizGO.AddComponent<ObjectQuizManager>();
            }
            else
            {
                Debug.Log("[NPCController] Found existing ObjectQuizManager");
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

            BeginProcessing();
            try
            {
                // Object Quiz mode: Check user's answer against the highlighted object
                if (_currentGameMode == ConversationGameMode.ObjectTagging && _objectQuizManager != null && _objectQuizManager.IsQuizActive)
                {
                    // First transcribe the user's answer
                    string userAnswer = await ExecuteWithTimeout(
                        _sttService.TranscribeAsync(audioClip),
                        GetSttTimeoutSeconds(),
                        "Transcription");

                    if (!string.IsNullOrWhiteSpace(userAnswer))
                    {
                        string targetLabel = _objectQuizManager.CurrentObjectLabel;
                        string safeLabel = string.IsNullOrWhiteSpace(targetLabel) ? "object" : targetLabel.Trim();
                        bool isCorrect = _objectQuizManager.SubmitAnswer(userAnswer);

                        string feedback;
                        if (isCorrect)
                        {
                            feedback = $"Correct! It's a {safeLabel}. Well done!";

                            if (avatarAnimationController != null)
                            {
                                avatarAnimationController.PlayClapping(3f);
                            }

                            _objectQuizManager.EndQuiz();

                            Speak(feedback);
                            await WaitForSpeechToFinishAsync();

                            if (TryStartObjectQuiz(announceStart: false))
                            {
                                Speak("What is this?");
                            }
                            else
                            {
                                StartObjectTaggingQuizWithRetry();
                            }
                        }
                        else
                        {
                            feedback = $"This is not correct. This is a {safeLabel}.";

                            Speak(feedback);
                            await WaitForSpeechToFinishAsync();

                            Speak($"Repeat after me: {safeLabel}.");
                            await WaitForSpeechToFinishAsync();
                        }
                    }

                    return;
                }

                if (_currentGameMode == ConversationGameMode.ObjectTagging && config != null)
                {
                    string languageName = GetLanguageName(config.conversation.language);
                    string systemPrompt = BuildGameModeSystemPrompt(_currentGameMode, languageName);
                    _currentAction = new ChatAction(systemPrompt);
                }

                var result = await ExecuteWithTimeout(
                    _conversationPipeline.ExecuteAsync(audioClip, _currentAction),
                    GetConversationPipelineTimeoutSeconds(),
                    "Conversation pipeline");

                if (result.Success && result.TTSAudioClip != null && DefaultAutoPlayTts)
                {
                    _lastTTSClip = result.TTSAudioClip;
                    PlayAudioWithEarlyStop(result.TTSAudioClip);
                }

                if (!result.Success && npcView != null)
                {
                    npcView.SetIdleState();
                    if (avatarAnimationController != null) avatarAnimationController.SetIdle();
                }
            }
            catch (Exception ex)
            {
                RecoverFromProcessingFailure($"Something went wrong while processing your speech. {GetSafeErrorMessage(ex)}");
            }
            finally
            {
                EndProcessing();
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
            _lastPipelineStage = stage;

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
        private void HandleLLMResponseReceived(string response)
        {
            if (DefaultShowSubtitles && npcView != null) npcView.ShowNPCMessage(response);

            // Check for positive keywords to trigger clapping
            if (!string.IsNullOrEmpty(response))
            {
                string lowerResponse = response.ToLowerInvariant();
                if (lowerResponse.Contains("good") ||
                    lowerResponse.Contains("great") ||
                    lowerResponse.Contains("perfect") ||
                    lowerResponse.Contains("well done"))
                {
                    if (avatarAnimationController != null)
                    {
                        avatarAnimationController.PlayClapping();
                    }
                }
            }
        }
        private void HandleTTSAudioGenerated(AudioClip audio) { }
        private void HandlePipelineError(string error)
        {
            if (npcView != null)
            {
                npcView.ShowErrorMessage(error);

                if (_lastPipelineStage == PipelineStage.SynthesizingSpeech)
                {
                    npcView.ShowTtsError(error);
                }
            }
        }

        private void Update()
        {
            if (_isProcessing && HasProcessingTimedOut())
            {
                RecoverFromProcessingFailure("Processing took too long and was reset. Please try speaking again.");
            }

            if (!_isProcessing && !_isWaitingForObjectTaggingObjects && _audioInput != null && !_audioInput.IsRecording && npcView != null && !npcView.IsAudioPlaying())
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
            var objectQuizHighlighter = FindObjectOfType<ObjectQuizHighlighter>();
            bool shouldEnable = (mode == ConversationGameMode.ObjectTagging);

            if (detectionRecorder != null)
            {
                detectionRecorder.SetDetectionEnabled(shouldEnable);
                detectionRecorder.SetVisualizerEnabled(shouldEnable);
                
                if (shouldEnable)
                    Debug.Log("[NPCController] Object detection ENABLED for ObjectTagging mode");
                else
                    Debug.Log($"[NPCController] Object detection DISABLED - Mode is {mode} (only enabled for ObjectTagging)");
            }

            if (objectQuizHighlighter != null)
            {
                objectQuizHighlighter.SetDetectionEnabled(shouldEnable);
            }
#endif

            // Object Quiz mode: Start quiz when entering ObjectTagging mode
            if (mode == ConversationGameMode.ObjectTagging && _objectQuizManager != null)
            {
                StartObjectTaggingQuizWithRetry();
            }
            else
            {
                StopObjectTaggingRetryLoop();

                if (_objectQuizManager != null && _objectQuizManager.IsQuizActive)
                {
                    // Leaving ObjectTagging mode - end any active quiz
                    _objectQuizManager.EndQuiz();
                }
            }

            // Word Building mode specific setup
            if (mode == ConversationGameMode.WordClouds)
            {
                ClearWordCloudsLetterBoxes();
                StartWordCloudsLetterBoxes();
            }
        }

        private void StartObjectTaggingQuizWithRetry()
        {
            StopObjectTaggingRetryLoop();

            if (_objectQuizManager == null)
            {
                return;
            }

            if (TryStartObjectQuiz(announceStart: true))
            {
                return;
            }

            Debug.LogWarning("[NPCController] No objects detected for quiz. Starting auto-retry loop.");
            Speak("I don't see any objects yet. Please look around so I can detect some objects.");

            var retryInterval = Mathf.Max(1f, objectTaggingRetryIntervalSeconds);
            _objectTaggingRetryRoutine = StartCoroutine(RetryObjectTaggingQuizLoop(retryInterval));
        }

        private bool TryStartObjectQuiz(bool announceStart)
        {
            if (_objectQuizManager == null)
            {
                return false;
            }

            if (!_objectQuizManager.StartQuiz())
            {
                return false;
            }

            StopObjectTaggingRetryLoop();
            Debug.Log("[NPCController] Started Object Quiz");

            if (announceStart)
            {
                Speak("Let's practice identifying objects! What is this?");
            }

            return true;
        }

        private System.Collections.IEnumerator RetryObjectTaggingQuizLoop(float retryInterval)
        {
            _isWaitingForObjectTaggingObjects = true;

            while (ShouldRetryObjectTaggingQuiz())
            {
                var secondsRemaining = Mathf.CeilToInt(retryInterval);
                while (secondsRemaining > 0)
                {
                    if (!ShouldRetryObjectTaggingQuiz())
                    {
                        _isWaitingForObjectTaggingObjects = false;
                        yield break;
                    }

                    if (npcView != null)
                    {
                        npcView.ShowStatusMessage($"No objects found, retrying in {secondsRemaining}s...");
                    }

                    yield return new WaitForSeconds(1f);
                    secondsRemaining--;
                }

                if (TryStartObjectQuiz(announceStart: true))
                {
                    _isWaitingForObjectTaggingObjects = false;
                    yield break;
                }
            }

            _isWaitingForObjectTaggingObjects = false;
        }

        private bool ShouldRetryObjectTaggingQuiz()
        {
            if (config == null || _objectQuizManager == null)
            {
                return false;
            }

            return config.conversation.gameMode == ConversationGameMode.ObjectTagging
                   && !_objectQuizManager.IsQuizActive;
        }

        private void StopObjectTaggingRetryLoop()
        {
            if (_objectTaggingRetryRoutine != null)
            {
                StopCoroutine(_objectTaggingRetryRoutine);
                _objectTaggingRetryRoutine = null;
            }

            _isWaitingForObjectTaggingObjects = false;
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
                //Speak("I don't see any detected objects yet. Please switch to Object Tagging mode first so I can detect objects. For now, we'll practice with CAT. Spell CAT by moving the letter blocks into the correct slots!");
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

        private async Task<T> ExecuteWithTimeout<T>(Task<T> task, float timeoutSeconds, string operationName)
        {
            int timeoutMs = Mathf.RoundToInt(Mathf.Max(1f, timeoutSeconds) * 1000f);
            Task timeoutTask = Task.Delay(timeoutMs);
            Task completedTask = await Task.WhenAny(task, timeoutTask);

            if (completedTask == task)
            {
                return await task;
            }

            CancelOngoingOperations();
            throw new TimeoutException($"{operationName} timed out after {timeoutSeconds:F1}s");
        }

        private void BeginProcessing()
        {
            _isProcessing = true;
            _processingStartedRealtime = Time.realtimeSinceStartup;
        }

        private void EndProcessing()
        {
            _isProcessing = false;
            _processingStartedRealtime = -1f;
        }

        private bool HasProcessingTimedOut()
        {
            if (!_isProcessing || _processingStartedRealtime < 0f)
            {
                return false;
            }

            float elapsed = Time.realtimeSinceStartup - _processingStartedRealtime;
            return elapsed > GetConversationPipelineTimeoutSeconds();
        }

        private float GetSttTimeoutSeconds()
        {
            float timeout = config != null ? config.stt.timeoutSeconds : 30f;
            return Mathf.Max(5f, timeout + ProcessingSafetyBufferSeconds);
        }

        private float GetConversationPipelineTimeoutSeconds()
        {
            if (config == null)
            {
                return 60f;
            }

            float timeout = config.stt.timeoutSeconds + config.llm.timeoutSeconds + config.tts.timeoutSeconds + ProcessingSafetyBufferSeconds;
            return Mathf.Max(MinimumPipelineTimeoutSeconds, timeout);
        }

        private void CancelOngoingOperations()
        {
            try
            {
                _sttService?.CancelTranscription();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NPCController] Failed to cancel STT operation: {ex.Message}");
            }

            try
            {
                _ttsService?.CancelSynthesis();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NPCController] Failed to cancel TTS operation: {ex.Message}");
            }
        }

        private void RecoverFromProcessingFailure(string userMessage)
        {
            Debug.LogError($"[NPCController] Recovery triggered: {userMessage}");

            CancelOngoingOperations();

            if (_audioInput != null && _audioInput.IsRecording)
            {
                _audioInput.CancelRecording();
            }

            if (npcView != null)
            {
                npcView.StopAudio();
                npcView.ShowErrorMessage(userMessage);
                npcView.SetIdleState();
            }

            if (avatarAnimationController != null)
            {
                avatarAnimationController.SetIdle();
            }

            EndProcessing();
            OnListeningStateChanged?.Invoke(false);
        }

        private string GetSafeErrorMessage(Exception ex)
        {
            if (ex is TimeoutException)
            {
                return "The request timed out.";
            }

            return "Please try again.";
        }

        private void UpdateTtsProviderStatusUi()
        {
            if (npcView == null || config == null)
            {
                return;
            }

            npcView.SetActiveTtsProvider(GetTtsProviderLabel(config.tts.provider));
        }

        private static string GetTtsProviderLabel(TTSSettings.TTSProvider provider)
        {
            switch (provider)
            {
                case TTSSettings.TTSProvider.ElevenLabs:
                    return "ElevenLabs";
                case TTSSettings.TTSProvider.AllTalk:
                    return "AllTalk";
                default:
                    return provider.ToString();
            }
        }
    }
}
