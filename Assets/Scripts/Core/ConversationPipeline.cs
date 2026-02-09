using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;
using LanguageTutor.Services.STT;
using LanguageTutor.Services.TTS;
using LanguageTutor.Services.LLM;
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
        private ConversationHistory _conversationHistory;
        private string _currentSystemPrompt;

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
                    ConversationHistory = GetRecentContextMessages(10),
                    SystemPrompt = _currentSystemPrompt
                };

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
        /// Update the active conversation history instance.
        /// </summary>
        public void SetConversationHistory(ConversationHistory history)
        {
            _conversationHistory = history ?? throw new ArgumentNullException(nameof(history));
        }

        /// <summary>
        /// Update the active system prompt for future requests.
        /// </summary>
        public void SetSystemPrompt(string systemPrompt)
        {
            _currentSystemPrompt = systemPrompt;
        }

        /// <summary>
        /// Get recent messages excluding system prompts for LLM context.
        /// </summary>
        public List<ConversationMessage> GetRecentContextMessages(int count)
        {
            var messages = _conversationHistory.GetRecentMessages(count);
            return FilterOutSystemMessages(messages);
        }

        private static List<ConversationMessage> FilterOutSystemMessages(List<ConversationMessage> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return new List<ConversationMessage>();
            }

            var filtered = new List<ConversationMessage>(messages.Count);
            foreach (var message in messages)
            {
                if (message.Role != MessageRole.System)
                {
                    filtered.Add(message);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Clear conversation history.
        /// </summary>
        public void ResetConversation(string systemPrompt = null, bool addSystemMessage = false)
        {
            _conversationHistory.Clear();
            if (addSystemMessage && !string.IsNullOrWhiteSpace(systemPrompt))
            {
                _conversationHistory.AddSystemMessage(systemPrompt);
            }

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
