using System;
using System.Threading.Tasks;
using UnityEngine;
using LanguageTutor.Services.STT;
using LanguageTutor.Services.TTS;
using LanguageTutor.Actions;

namespace LanguageTutor.Core
{
    /// <summary>
    /// Orchestrates the complete conversation pipeline:
    /// Speech-to-Text → LLM Action → Text-to-Speech
    /// </summary>
    public class ConversationPipeline
    {
        private readonly ISTTService _sttService;
        private readonly ITTSService _ttsService;
        private readonly LLMActionExecutor _actionExecutor;
        private readonly ConversationHistory _conversationHistory;

        public ConversationHistory History => _conversationHistory;

        public event Action<string> OnTranscriptionCompleted;
        public event Action<string> OnLLMResponseReceived;
        public event Action<AudioClip> OnTTSAudioGenerated;
        public event Action<string> OnPipelineError;
        public event Action<PipelineStage> OnStageChanged;

        public ConversationPipeline(
            ISTTService sttService,
            ITTSService ttsService,
            LLMActionExecutor actionExecutor,
            ConversationHistory conversationHistory)
        {
            _sttService = sttService ?? throw new ArgumentNullException(nameof(sttService));
            _ttsService = ttsService ?? throw new ArgumentNullException(nameof(ttsService));
            _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
            _conversationHistory = conversationHistory ?? throw new ArgumentNullException(nameof(conversationHistory));
        }

        /// <summary>
        /// Execute the full conversation pipeline from audio input to audio output.
        /// </summary>
        public async Task<PipelineResult> ExecuteAsync(AudioClip userAudio, ILLMAction action)
        {
            var result = new PipelineResult();

            try
            {
                // Stage 1: Speech-to-Text
                OnStageChanged?.Invoke(PipelineStage.Transcribing);
                Debug.Log("[ConversationPipeline] Stage 1: Transcribing speech...");

                string transcribedText = await _sttService.TranscribeAsync(userAudio);

                if (string.IsNullOrWhiteSpace(transcribedText))
                {
                    OnPipelineError?.Invoke("Transcription resulted in empty text");
                    result.Success = false;
                    result.ErrorMessage = "Could not understand speech";
                    return result;
                }

                result.TranscribedText = transcribedText;
                OnTranscriptionCompleted?.Invoke(transcribedText);
                _conversationHistory.AddUserMessage(transcribedText);

                Debug.Log($"[ConversationPipeline] Transcribed: {transcribedText}");

                // Stage 2: LLM Processing
                OnStageChanged?.Invoke(PipelineStage.GeneratingResponse);
                Debug.Log("[ConversationPipeline] Stage 2: Generating LLM response...");

                var context = new LLMActionContext(transcribedText)
                {
                    ConversationHistory = _conversationHistory.GetRecentMessages(10)
                };

                // INTEGRATION: Add room context to the LLM (detected objects in the room)
                var roomContext = UnityEngine.Object.FindObjectOfType<TutorContextComponent>();
                Debug.Log($"[ConversationPipeline] Checking room context... TutorContextComponent: {(roomContext != null ? "FOUND" : "NULL")}");

                if (roomContext != null && roomContext.HasDetectedObjects())
                {
                    // Enrich the system prompt with room awareness
                    string roomContextText = roomContext.BuildTutorSystemPromptContext();
                    context.SystemPrompt = roomContextText;
                    Debug.Log($"[ConversationPipeline] ✓ Room context ADDED. Detected {roomContext.GetTotalDetectedObjectCount()} objects.");
                }
                else if (roomContext == null)
                {
                    Debug.LogWarning("[ConversationPipeline] ✗ TutorContextComponent NOT FOUND in scene. Add it to your NPC GameObject!");
                }
                else
                {
                    Debug.LogWarning($"[ConversationPipeline] ✗ TutorContextComponent found but 0 objects detected. Registry has {roomContext.GetTotalDetectedObjectCount()} entries.");
                }

                var actionResult = await _actionExecutor.ExecuteAsync(action, context);

                if (!actionResult.Success)
                {
                    OnPipelineError?.Invoke(actionResult.ErrorMessage);
                    result.Success = false;
                    result.ErrorMessage = actionResult.ErrorMessage;
                    return result;
                }

                result.LLMResponse = actionResult.ResponseText;
                OnLLMResponseReceived?.Invoke(actionResult.ResponseText);
                _conversationHistory.AddAssistantMessage(actionResult.ResponseText);

                Debug.Log($"[ConversationPipeline] LLM Response: {actionResult.ResponseText}");

                // Stage 3: Text-to-Speech
                OnStageChanged?.Invoke(PipelineStage.SynthesizingSpeech);
                Debug.Log("[ConversationPipeline] Stage 3: Synthesizing speech...");

                AudioClip ttsAudio = await _ttsService.SynthesizeSpeechAsync(actionResult.ResponseText);

                if (ttsAudio == null)
                {
                    OnPipelineError?.Invoke("TTS generation failed");
                    result.Success = false;
                    result.ErrorMessage = "Failed to generate speech audio";
                    return result;
                }

                result.TTSAudioClip = ttsAudio;
                OnTTSAudioGenerated?.Invoke(ttsAudio);

                Debug.Log($"[ConversationPipeline] TTS audio generated: {ttsAudio.length:F2}s");

                // Success!
                OnStageChanged?.Invoke(PipelineStage.Complete);
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConversationPipeline] Pipeline error: {ex.Message}");
                OnPipelineError?.Invoke(ex.Message);
                OnStageChanged?.Invoke(PipelineStage.Error);

                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Execute only transcription (useful for practice without LLM).
        /// </summary>
        public async Task<string> TranscribeOnlyAsync(AudioClip userAudio)
        {
            try
            {
                OnStageChanged?.Invoke(PipelineStage.Transcribing);
                string transcribedText = await _sttService.TranscribeAsync(userAudio);
                OnTranscriptionCompleted?.Invoke(transcribedText);
                return transcribedText;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConversationPipeline] Transcription error: {ex.Message}");
                OnPipelineError?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Execute only TTS (useful for speaking pre-defined text).
        /// </summary>
        public async Task<AudioClip> SynthesizeSpeechOnlyAsync(string text)
        {
            try
            {
                OnStageChanged?.Invoke(PipelineStage.SynthesizingSpeech);
                AudioClip audio = await _ttsService.SynthesizeSpeechAsync(text);
                OnTTSAudioGenerated?.Invoke(audio);
                return audio;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConversationPipeline] TTS error: {ex.Message}");
                OnPipelineError?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Clear conversation history.
        /// </summary>
        public void ResetConversation()
        {
            _conversationHistory.Clear();
            Debug.Log("[ConversationPipeline] Conversation reset");
        }
    }

    /// <summary>
    /// Result of a complete conversation pipeline execution.
    /// </summary>
    public class PipelineResult
    {
        public bool Success { get; set; }
        public string TranscribedText { get; set; }
        public string LLMResponse { get; set; }
        public AudioClip TTSAudioClip { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Stages of the conversation pipeline.
    /// </summary>
    public enum PipelineStage
    {
        Idle,
        Transcribing,
        GeneratingResponse,
        SynthesizingSpeech,
        Complete,
        Error
    }
}
