using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;
using LanguageTutor.Services.LLM;
using LanguageTutor.Services.STT;
using LanguageTutor.Services.TTS;
using LanguageTutor.Services.Vision;
using LanguageTutor.Actions;
using LanguageTutor.Data;
using LanguageTutor.UI;
using Meta.XR;

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

        [Header("Vision / Object Tagging")]
        [SerializeField] private PassthroughCameraAccess passthroughCameraAccess;
        [SerializeField] private float objectTaggingCaptureDelaySeconds = 0.5f;

        [Header("Action Mode")]
        [SerializeField] private ActionMode defaultActionMode = ActionMode.Chat;

        // Services
        private ILLMService _llmService;
        private ITTSService _ttsService;
        private ISTTService _sttService;
        private IVisionService _visionService;

        // Core Systems
        private AudioInputController _audioInput;
        private ConversationPipeline _conversationPipeline;
        private LLMActionExecutor _actionExecutor;
        private readonly Dictionary<string, ConversationHistory> _historyByMode = new Dictionary<string, ConversationHistory>();
        private RoleplayScenarioConfig _activeRoleplayScenario;
        private string _currentSystemPrompt;

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
            _visionService = new OpenAIVisionService(llmConfig, this);

            Debug.Log($"[NPCController] Services initialized - LLM: {_llmService.GetModelName()}, TTS: {ttsConfig.provider}, STT: {sttConfig.provider}");
        }

        /// <summary>
        /// Initialize core systems using the services.
        /// </summary>
        private void InitializeSystems()
        {
            var initialHistory = CreateConversationHistory();

            _historyByMode.Clear();
            _historyByMode[GetHistoryKey(defaultActionMode, null)] = initialHistory;

            _actionExecutor = new LLMActionExecutor(
                _llmService,
                llmConfig.maxRetries,
                llmConfig.retryDelaySeconds
            );

            _conversationPipeline = new ConversationPipeline(
                _sttService,
                _ttsService,
                _actionExecutor,
                initialHistory
            );

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

            if (defaultActionMode == ActionMode.ObjectTaggingVision)
            {
                await HandleObjectTaggingVisionAsync(audioClip);
                _isProcessing = false;
                return;
            }

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
            // In Word Reordering mode, show the scrambled sentence in subtitles
            // (the response text is already the scrambled version)
            if (conversationConfig.showSubtitles)
            {
                npcView.ShowNPCMessage(response);
            }

            // Handle Word Reordering mode — display word bubbles above NPC head
            if (_currentAction is WordReorderingAction wordAction)
            {
                WordReorderingGameController gameController = FindObjectOfType<WordReorderingGameController>();
                if (gameController != null)
                {
                    gameController.DisplayWordReorderingGame(wordAction);
                    Debug.Log($"[NPCController] Word cloud game triggered — correct sentence: {wordAction.CurrentSentence}");
                }
                else
                {
                    Debug.LogWarning("[NPCController] WordReorderingGameController not found in scene!");
                }
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

        #region Vision / Object Tagging

        private async Task HandleObjectTaggingVisionAsync(AudioClip audioClip)
        {
            if (_visionService == null)
            {
                npcView.ShowErrorMessage("Vision service not initialized");
                return;
            }

            try
            {
                npcView.SetProcessingState("Transcribing...");
                if (avatarAnimationController != null)
                    avatarAnimationController.SetThinking();

                string transcribedText = await _conversationPipeline.TranscribeOnlyAsync(audioClip);
                if (string.IsNullOrWhiteSpace(transcribedText))
                {
                    npcView.ShowErrorMessage("Could not understand speech");
                    return;
                }

                Debug.Log($"[NPCController] Object Tagging STT: {transcribedText}");

                if (conversationConfig.showSubtitles)
                {
                    npcView.ShowUserMessage(transcribedText);
                }

                _conversationPipeline.History.AddUserMessage(transcribedText);

                npcView.SetProcessingState("Capturing image...");
                Texture2D capturedTexture = await CapturePassthroughFrameAsync();
                if (capturedTexture == null)
                {
                    npcView.ShowErrorMessage("Failed to capture passthrough frame");
                    return;
                }

                string framePath = VisionDebugLogger.SaveFrame(capturedTexture);
                if (!string.IsNullOrWhiteSpace(framePath))
                {
                    Debug.Log($"[NPCController] Captured frame saved: {framePath}");
                }

                try
                {
                    npcView.SetProcessingState("Analyzing image...");

                    string systemPrompt = llmConfig != null
                        ? llmConfig.objectTaggingVisionPrompt
                        : "";

                    string responseText = await _visionService.GenerateResponseAsync(transcribedText, capturedTexture, systemPrompt);
                    if (string.IsNullOrWhiteSpace(responseText))
                    {
                        npcView.ShowErrorMessage("Vision model returned an empty response");
                        return;
                    }

                    Debug.Log($"[NPCController] VLM response: {responseText}");

                    npcView.ShowNPCMessage(responseText);
                    _conversationPipeline.History.AddAssistantMessage(responseText);

                    AudioClip ttsAudio = await _conversationPipeline.SynthesizeSpeechOnlyAsync(responseText);
                    if (ttsAudio != null && conversationConfig.autoPlayTTS)
                    {
                        Debug.Log($"[NPCController] TTS ready (Object Tagging): {ttsAudio.length:F2}s");
                        _lastTTSClip = ttsAudio;
                        npcView.PlayAudio(ttsAudio);
                        npcView.SetSpeakingState();

                        if (avatarAnimationController != null)
                            avatarAnimationController.SetTalking();
                    }
                }
                finally
                {
                    Destroy(capturedTexture);
                }
            }
            catch (Exception ex)
            {
                npcView.ShowErrorMessage($"Vision request failed: {ex.Message}");
            }
        }

        private Task<Texture2D> CapturePassthroughFrameAsync()
        {
            var tcs = new TaskCompletionSource<Texture2D>();
            StartCoroutine(CapturePassthroughFrameCoroutine(tcs));
            return tcs.Task;
        }

        private System.Collections.IEnumerator CapturePassthroughFrameCoroutine(TaskCompletionSource<Texture2D> tcs)
        {
            if (!TryResolveCameraAccess(out var access) || !access.IsPlaying)
            {
                tcs.SetResult(null);
                yield break;
            }

            if (objectTaggingCaptureDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(objectTaggingCaptureDelaySeconds);
            }

            yield return new WaitForEndOfFrame();

            var texture = CapturePassthroughFrame(access);
            tcs.SetResult(texture);
        }

        private bool TryResolveCameraAccess(out PassthroughCameraAccess access)
        {
            access = passthroughCameraAccess ? passthroughCameraAccess : FindAnyObjectByType<PassthroughCameraAccess>();
            passthroughCameraAccess = access;
            return access != null;
        }

        private static Texture2D CapturePassthroughFrame(PassthroughCameraAccess access)
        {
            var resolution = access.CurrentResolution;
            if (resolution == Vector2Int.zero)
                return null;

            var colors = access.GetColors();
            if (!colors.IsCreated)
                return null;

            var texture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
            texture.SetPixelData(colors, 0);
            texture.Apply();
            return texture;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Create a new conversation history instance.
        /// </summary>
        private ConversationHistory CreateConversationHistory()
        {
            return new ConversationHistory(
                conversationConfig.maxHistoryLength,
                conversationConfig.autoSummarizeHistory
            );
        }

        private string GetHistoryKey(ActionMode mode, RoleplayScenarioConfig scenario)
        {
            if (mode == ActionMode.ConversationPractice && scenario != null)
            {
                return $"Roleplay:{scenario.GetInstanceID()}";
            }

            return mode.ToString();
        }

        private string GetSystemPromptForMode(ActionMode mode, RoleplayScenarioConfig scenario)
        {
            if (mode == ActionMode.ConversationPractice && scenario != null)
            {
                return scenario.systemPrompt;
            }

            switch (mode)
            {
                case ActionMode.Chat:
                    return llmConfig.defaultSystemPrompt;
                case ActionMode.GrammarCheck:
                    return llmConfig.grammarCorrectionPrompt;
                case ActionMode.VocabularyTeach:
                    return llmConfig.vocabularyTeachingPrompt;
                case ActionMode.ConversationPractice:
                    return llmConfig.conversationPracticePrompt;
                case ActionMode.WordReordering:
                    return llmConfig.wordReorderingPrompt;
                case ActionMode.ObjectTaggingVision:
                    return llmConfig.objectTaggingVisionPrompt;
                default:
                    return llmConfig.defaultSystemPrompt;
            }
        }

        private void ApplyModeContext(ActionMode mode, RoleplayScenarioConfig scenario, string systemPrompt)
        {
            _currentSystemPrompt = systemPrompt;

            string key = GetHistoryKey(mode, scenario);
            if (!_historyByMode.TryGetValue(key, out var history))
            {
                history = CreateConversationHistory();
                _historyByMode[key] = history;
            }

            _conversationPipeline.SetConversationHistory(history);
            _conversationPipeline.SetSystemPrompt(systemPrompt);
            _conversationPipeline.ResetConversation(systemPrompt, true);
        }

        /// <summary>
        /// Set the current action mode for conversation.
        /// </summary>
        public void SetActionMode(ActionMode mode)
        {
            defaultActionMode = mode;
            _activeRoleplayScenario = null;

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
                case ActionMode.WordReordering:
                    _currentAction = new WordReorderingAction(llmConfig.wordReorderingPrompt);
                    break;
                case ActionMode.ObjectTaggingVision:
                    _currentAction = null;
                    break;
            }

            string systemPrompt = GetSystemPromptForMode(mode, null);
            ApplyModeContext(mode, null, systemPrompt);

            Debug.Log($"[NPCController] Action mode set to: {mode}");
        }

        /// <summary>
        /// Reset conversation history.
        /// </summary>
        public void ResetConversation()
        {
            _conversationPipeline.ResetConversation(_currentSystemPrompt, true);
            _lastTTSClip = null;
            npcView.ClearSubtitle();
            npcView.ShowStatusMessage("Conversation reset");
        }

        /// <summary>
        /// Get conversation summary.
        /// </summary>
        public ConversationSummary GetConversationSummary()
        {
            return _conversationPipeline.History.GetSummary();
        }

        public void SetTTSSpeed(float speed)
        {
            if (_ttsService != null)
            {
                _ttsService.SetSpeed(speed);
            }
        }

        /// <summary>
        /// Set a roleplay scenario for conversation practice mode.
        /// </summary>
        public void SetRoleplayScenario(Data.RoleplayScenarioConfig scenario)
        {
            if (scenario == null)
            {
                Debug.LogWarning("[NPCController] Roleplay scenario is null, using default conversation practice");
                SetActionMode(ActionMode.ConversationPractice);
                return;
            }

            _activeRoleplayScenario = scenario;

            // Create a conversation practice action with the roleplay scenario
            string scenarioDescription = $"{scenario.aiRole} at {scenario.setting}";
            if (!string.IsNullOrEmpty(scenario.additionalContext))
            {
                scenarioDescription += $". {scenario.additionalContext}";
            }

            _currentAction = new ConversationPracticeAction(scenarioDescription, scenario.systemPrompt);
            defaultActionMode = ActionMode.ConversationPractice;

            string systemPrompt = GetSystemPromptForMode(ActionMode.ConversationPractice, scenario);
            ApplyModeContext(ActionMode.ConversationPractice, scenario, systemPrompt);

            Debug.Log($"[NPCController] Roleplay scenario set: {scenario.scenarioName} - {scenarioDescription}");

            // Optionally show a greeting from the AI character
            if (npcView != null)
            {
                npcView.ShowStatusMessage($"Roleplay: {scenario.scenarioName}");
            }
        }

        // ──────────────────────────────────────────────
        // Word Reordering helpers (called by WordReorderingGameController)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Create a fresh WordReorderingAction using the currently configured prompt.
        /// </summary>
        public WordReorderingAction CreateWordReorderingAction()
        {
            return new WordReorderingAction(llmConfig.wordReorderingPrompt);
        }

        /// <summary>
        /// Execute a word reordering round: send a prompt to the LLM via the action executor,
        /// process the response, and return success/failure.
        /// Does NOT play TTS — the game controller handles display.
        /// </summary>
        public async Task<bool> ExecuteWordReorderingRoundAsync(WordReorderingAction action, string userPrompt)
        {
            if (action == null || _actionExecutor == null)
                return false;

            try
            {
                var context = new LLMActionContext(userPrompt)
                {
                    ConversationHistory = _conversationPipeline.GetRecentContextMessages(5),
                    SystemPrompt = llmConfig.wordReorderingPrompt,
                    TargetLanguage = conversationConfig.targetLanguage,
                    UserLevel = conversationConfig.userLevel.ToString()
                };

                var result = await _actionExecutor.ExecuteAsync(action, context);

                if (result.Success)
                {
                    Debug.Log($"[NPCController] Word reordering round generated: {action.CurrentSentence}");
                    return true;
                }

                Debug.LogError($"[NPCController] Word reordering generation failed: {result.ErrorMessage}");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NPCController] Word reordering round error: {ex.Message}");
                return false;
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
        ConversationPractice,
        WordReordering,
        ObjectTaggingVision
    }
}
