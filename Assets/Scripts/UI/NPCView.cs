using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

namespace LanguageTutor.UI
{
    /// <summary>
    /// Manages the visual presentation of NPC conversation.
    /// Handles UI updates and audio playback without business logic.
    /// </summary>
    public class NPCView : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private ScrollingSubtitles scrollingSubtitles;
        [SerializeField] private EventLog eventLog;
        [SerializeField] private TextMeshProUGUI subtitleText; // Legacy - kept for backward compatibility
        [SerializeField] private TextMeshProUGUI statusText; // Displays current system status
        [SerializeField] private Button talkButton;
        [SerializeField] private Image statusIndicator;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        [Header("Visual Feedback")]
        [SerializeField] private Color listeningColor = Color.yellow;
        [SerializeField] private Color processingColor = Color.blue;
        [SerializeField] private Color speakingColor = Color.green;
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color errorColor = Color.red;

        [Header("Conversation Settings")]
        [SerializeField] private int maxConversationLines = 10;
        [SerializeField] private bool showConversationHistory = true;
        [SerializeField] private bool showStatusInConversation = false;

        private string _currentSubtitle;
        private string _conversationText;
        private List<string> _conversationHistory = new List<string>();

        private void Awake()
        {
            ValidateComponents();
        }

        private void ValidateComponents()
        {
            if (scrollingSubtitles == null && subtitleText == null)
                Debug.LogWarning("[NPCView] Neither ScrollingSubtitles nor SubtitleText is assigned! Subtitle display will not work.");
            
            if (eventLog == null)
                Debug.LogWarning("[NPCView] EventLog is not assigned! Events will not be logged.");
            
            if (audioSource == null)
                Debug.LogError("[NPCView] AudioSource is not assigned!");
            
            if (talkButton == null)
                Debug.LogError("[NPCView] TalkButton is not assigned!");
        }

        /// <summary>
        /// Display user's transcribed speech.
        /// </summary>
        public void ShowUserMessage(string message)
        {
            Debug.Log($"[NPCView] ShowUserMessage called. Message: {message}");
            
            // Log to event log
            if (eventLog != null)
            {
                eventLog.LogInfo($"User said: {message}");
            }
            
            // Use new scrolling subtitle system if available
            if (scrollingSubtitles != null)
            {
                scrollingSubtitles.AddUserMessage(message);
            }
            // Fallback to legacy system
            else if (showConversationHistory)
            {
                AddToConversationHistory($"<color=blue>User:</color> {message}");
                UpdateSubtitle();
            }
            else
            {
                _currentSubtitle = $"<color=blue>User:</color> {message}";
                UpdateSubtitle();
            }
        }

        /// <summary>
        /// Display tutor's response.
        /// </summary>
        public void ShowNPCMessage(string message)
        {
            Debug.Log($"[NPCView] ShowNPCMessage called. Message: {message}");
            
            // Log to event log
            if (eventLog != null)
            {
                eventLog.LogInfo($"Tutor said: {message}");
            }
            
            // Use new scrolling subtitle system if available
            if (scrollingSubtitles != null)
            {
                scrollingSubtitles.AddTutorMessage(message);
            }
            // Fallback to legacy system
            else if (showConversationHistory)
            {
                AddToConversationHistory($"<color=green>Tutor:</color> {message}");
                UpdateSubtitle();
            }
            else
            {
                _currentSubtitle = $"<color=green>Tutor:</color> {message}";
                UpdateSubtitle();
            }
        }

        /// <summary>
        /// Display system/status message.
        /// </summary>
        public void ShowStatusMessage(string message)
        {
            // Don't log every status message to event log - too verbose
            // Only important events are logged from specific methods
            
            // If statusText is assigned, use it for status
            if (statusText != null)
            {
                statusText.text = message;
            }
            
            // Also show in scrolling subtitles if available (non-intrusive)
            if (scrollingSubtitles != null && showStatusInConversation)
            {
                // Don't add every status update to avoid clutter
                // Only add significant status messages
                // scrollingSubtitles.AddStatusMessage(message);
            }
            else if (showStatusInConversation)
            {
                // Show status in subtitle area without clearing conversation
                _currentSubtitle = $"{_conversationText}\n\n<color=yellow><i>{message}</i></color>";
                UpdateSubtitle();
            }
            // Otherwise, don't overwrite the conversation
        }

        /// <summary>
        /// Display error message.
        /// </summary>
        public void ShowErrorMessage(string error)
        {
            // Log errors to event log only, not to subtitles
            if (eventLog != null)
            {
                eventLog.LogError(error);
            }
            else
            {
                // Fallback to subtitle if no event log
                if (scrollingSubtitles != null)
                {
                    scrollingSubtitles.AddErrorMessage(error);
                }
                else if (showConversationHistory)
                {
                    AddToConversationHistory($"<color=red>Error:</color> {error}");
                    UpdateSubtitle();
                }
                else
                {
                    _currentSubtitle = $"<color=red>Error:</color> {error}";
                    UpdateSubtitle();
                }
            }
            SetStatusColor(errorColor);
        }

        /// <summary>
        /// Clear subtitle text.
        /// </summary>
        public void ClearSubtitle()
        {
            // Clear new scrolling subtitle system
            if (scrollingSubtitles != null)
            {
                scrollingSubtitles.ClearSubtitles();
            }
            
            // Clear legacy system
            _currentSubtitle = string.Empty;
            _conversationText = string.Empty;
            _conversationHistory.Clear();
            UpdateSubtitle();
            if (statusText != null)
            {
                statusText.text = string.Empty;
            }
        }

        /// <summary>
        /// Add a line to the conversation history (legacy system).
        /// </summary>
        private void AddToConversationHistory(string line)
        {
            _conversationHistory.Add(line);
            Debug.Log($"[NPCView] Added to legacy history. Total messages: {_conversationHistory.Count}");
            
            // Keep only the last N lines
            if (_conversationHistory.Count > maxConversationLines)
            {
                _conversationHistory.RemoveAt(0);
            }
            
            // Build the full conversation text
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _conversationHistory.Count; i++)
            {
                sb.AppendLine(_conversationHistory[i]);
                if (i < _conversationHistory.Count - 1)
                {
                    sb.AppendLine(); // Add extra line break between messages
                }
            }
            
            _conversationText = sb.ToString();
            _currentSubtitle = _conversationText;
            Debug.Log($"[NPCView] Conversation text built: {_conversationText.Length} characters, {_conversationHistory.Count} messages");
        }

        /// <summary>
        /// Play audio clip through the audio source.
        /// </summary>
        public void PlayAudio(AudioClip clip)
        {
            if (audioSource == null)
            {
                Debug.LogError("[NPCView] Cannot play audio - AudioSource is null! Make sure AudioSource component is assigned.");
                if (eventLog != null)
                {
                    eventLog.LogError("Audio playback failed - AudioSource not assigned");
                }
                return;
            }
            
            if (clip == null)
            {
                Debug.LogError("[NPCView] Cannot play audio - AudioClip is null!");
                if (eventLog != null)
                {
                    eventLog.LogError("Audio playback failed - AudioClip is null");
                }
                return;
            }

            Debug.Log($"[NPCView] Playing audio clip: {clip.name}, length: {clip.length}s, samples: {clip.samples}, channels: {clip.channels}, frequency: {clip.frequency}");
            Debug.Log($"[NPCView] Audio Source - Volume: {audioSource.volume}, Mute: {audioSource.mute}, Spatial Blend: {audioSource.spatialBlend}");
            Debug.Log($"[NPCView] Unity Audio - Sample Rate: {AudioSettings.outputSampleRate}, Speaker Mode: {AudioSettings.speakerMode}");
            
            // Audio playback logged separately - don't duplicate here
            
            audioSource.clip = clip;
            audioSource.Play();
            SetStatusColor(speakingColor);
            
            Debug.Log($"[NPCView] Audio is playing: {audioSource.isPlaying}, Time: {audioSource.time}");
            
            // Check if Audio Listener exists
            var listener = FindObjectOfType<AudioListener>();
            if (listener == null)
            {
                Debug.LogError("[NPCView] NO AUDIO LISTENER FOUND IN SCENE! Add AudioListener component to Main Camera!");
            }
            else
            {
                Debug.Log($"[NPCView] Audio Listener found on: {listener.gameObject.name}");
            }
        }

        /// <summary>
        /// Stop currently playing audio.
        /// </summary>
        public void StopAudio()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        /// <summary>
        /// Check if audio is currently playing.
        /// </summary>
        public bool IsAudioPlaying()
        {
            return audioSource != null && audioSource.isPlaying;
        }

        /// <summary>
        /// Set the button interactable state.
        /// </summary>
        public void SetButtonInteractable(bool interactable)
        {
            if (talkButton != null)
            {
                talkButton.interactable = interactable;
            }
        }

        /// <summary>
        /// Set button text.
        /// </summary>
        public void SetButtonText(string text)
        {
            if (talkButton != null)
            {
                var buttonText = talkButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = text;
                }
            }
        }

        /// <summary>
        /// Set visual state for listening.
        /// </summary>
        public void SetListeningState()
        {
            if (eventLog != null)
            {
                eventLog.LogSystem("Started listening for user input");
            }
            ShowStatusMessage("Listening...");
            SetStatusColor(listeningColor);
            SetButtonText("Stop");
        }

        /// <summary>
        /// Set visual state for processing.
        /// </summary>
        public void SetProcessingState(string stage)
        {
            // Don't log processing stages - too verbose
            ShowStatusMessage(stage);
            SetStatusColor(processingColor);
            SetButtonInteractable(false);
        }

        /// <summary>
        /// Set visual state for speaking.
        /// </summary>
        public void SetSpeakingState()
        {
            // Don't log - audio playback is already logged
            ShowStatusMessage("Tutor is speaking...");
            SetStatusColor(speakingColor);
        }

        /// <summary>
        /// Set visual state for idle/ready.
        /// </summary>
        public void SetIdleState()
        {
            // Don't log every time we return to idle - too verbose
            ShowStatusMessage("Ready");
            SetStatusColor(idleColor);
            SetButtonInteractable(true);
            SetButtonText("Talk");
        }

        /// <summary>
        /// Set status indicator color.
        /// </summary>
        private void SetStatusColor(Color color)
        {
            if (statusIndicator != null)
            {
                statusIndicator.color = color;
            }
        }

        /// <summary>
        /// Update subtitle text component.
        /// </summary>
        private void UpdateSubtitle()
        {
            if (subtitleText != null)
            {
                subtitleText.text = _currentSubtitle;
            }
        }

        /// <summary>
        /// Get the talk button component for adding listeners.
        /// </summary>
        public Button GetTalkButton()
        {
            return talkButton;
        }

        /// <summary>
        /// Get the audio source component.
        /// </summary>
        public AudioSource GetAudioSource()
        {
            return audioSource;
        }
    }
}
