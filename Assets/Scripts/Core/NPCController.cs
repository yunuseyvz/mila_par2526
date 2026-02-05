using UnityEngine;
using Whisper;
using LanguageTutor.Services.LLM;
using LanguageTutor.Services.STT;
using LanguageTutor.Services.TTS;
using LanguageTutor.Actions;
using LanguageTutor.Data;
using LanguageTutor.UI;
using System.Threading.Tasks;

namespace LanguageTutor.Core
{
// Enum ganz oben definiert für Sichtbarkeit
public enum ActionMode
{
Chat,
GrammarCheck,
VocabularyTeach,
ConversationPractice,
SpellingGame
}

/// <summary>
/// Main controller for the NPC conversation system.
/// Orchestrates AudioInput, ConversationPipeline, and NPCView.
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

private ILLMAction _currentAction;
private AudioClip _lastTTSClip;
private bool _isProcessing;

private void Start()
{
    InitializeServices();
    InitializeSystems();
    SetupEventListeners();
    SetDefaultAction();

    if (npcView != null) npcView.SetIdleState();

    // Begrüßung (Original-Funktion)
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

    // 1. Animation starten
    if (avatarAnimationController != null)
        avatarAnimationController.SetTalking();

    // 2. Untertitel anzeigen
    if (npcView != null)
        npcView.ShowNPCMessage(text);

    // 3. Audio generieren (KORRIGIERT: Nutzt jetzt SynthesizeSpeechAsync)
    if (_ttsService != null)
    {
        // Hier war der Fehler: Wir rufen jetzt die Methode auf, die in deinem Interface steht!
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

        // SPIEL LOGIK: Sofort-Start
        case ActionMode.SpellingGame:
            var gameAction = new SpellingGameAction();
            _currentAction = gameAction;

            Debug.Log("[NPCController] Spelling Game selected. Auto-starting...");

            // Spiel sofort starten (Fire-and-Forget)
            _ = gameAction.ExecuteAsync(null, null);
            break;
    }

    Debug.Log($"[NPCController] Action mode set to: {mode}");
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

// -------------------------------------------------------------------------
// SECTION: Initialization & Event Setup
// -------------------------------------------------------------------------

private void InitializeServices()
{
    if (llmConfig == null || ttsConfig == null || sttConfig == null || conversationConfig == null)
    {
        Debug.LogError("[NPCController] Configuration missing!");
        enabled = false;
        return;
    }

    if (STTServiceFactory.RequiresWhisperManager(sttConfig.provider) && whisperManager == null)
    {
        Debug.LogError("[NPCController] WhisperManager missing.");
        enabled = false;
        return;
    }

    _llmService = LLMServiceFactory.CreateService(llmConfig, this);
    _ttsService = TTSServiceFactory.CreateService(ttsConfig, this);
    _sttService = STTServiceFactory.CreateService(sttConfig, this, whisperManager);
}

private void InitializeSystems()
{
    _conversationHistory = new ConversationHistory(conversationConfig.maxHistoryLength, conversationConfig.autoSummarizeHistory);
    _actionExecutor = new LLMActionExecutor(_llmService, llmConfig.maxRetries, llmConfig.retryDelaySeconds);
    _conversationPipeline = new ConversationPipeline(_sttService, _ttsService, _actionExecutor, _conversationHistory);
    _audioInput = new AudioInputController(sttConfig.maxRecordingDuration, sttConfig.sampleRate, sttConfig.silenceThreshold);
}

private void SetupEventListeners()
{
    if (npcView != null && npcView.GetTalkButton() != null)
        npcView.GetTalkButton().onClick.AddListener(OnTalkButtonPressed);

    if (_audioInput != null)
    {
        _audioInput.OnRecordingStarted += HandleRecordingStarted;
        _audioInput.OnRecordingCompleted += HandleRecordingCompleted;
        _audioInput.OnRecordingError += HandleRecordingError;
    }

    if (_conversationPipeline != null)
    {
        _conversationPipeline.OnStageChanged += HandlePipelineStageChanged;
        _conversationPipeline.OnTranscriptionCompleted += HandleTranscriptionCompleted;
        _conversationPipeline.OnLLMResponseReceived += HandleLLMResponseReceived;
        _conversationPipeline.OnTTSAudioGenerated += HandleTTSAudioGenerated;
        _conversationPipeline.OnPipelineError += HandlePipelineError;
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
    SetActionMode(defaultActionMode);
}

private void OnTalkButtonPressed()
{
    if (_isProcessing || _audioInput == null) return;
    _audioInput.ToggleRecording();
}

// -------------------------------------------------------------------------
// SECTION: Event Handlers
// -------------------------------------------------------------------------

private void HandleRecordingStarted()
{
    if (npcView != null) npcView.SetListeningState();
}

private async void HandleRecordingCompleted(AudioClip audioClip)
{
    if (audioClip == null) return;
    _isProcessing = true;

    // Standard Pipeline für Antworten auf den Spieler
    var result = await _conversationPipeline.ExecuteAsync(audioClip, _currentAction);

    if (result.Success && result.TTSAudioClip != null && conversationConfig.autoPlayTTS)
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

private void HandleTranscriptionCompleted(string text) { if (conversationConfig.showSubtitles && npcView != null) npcView.ShowUserMessage(text); }
private void HandleLLMResponseReceived(string response) { if (conversationConfig.showSubtitles && npcView != null) npcView.ShowNPCMessage(response); }
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
}
}