using System;
using UnityEngine;

namespace LanguageTutor.Core
{
    /// <summary>
    /// Handles microphone input and audio recording for speech recognition.
    /// Separated from NPCController for single responsibility.
    /// </summary>
    public class AudioInputController
    {
        private readonly int _maxRecordingDuration;
        private readonly int _sampleRate;
        private readonly float _silenceThreshold;

        private bool _isRecording;
        private AudioClip _recordingClip;
        private string _microphoneDevice;

        public bool IsRecording => _isRecording;
        public string MicrophoneDevice => _microphoneDevice;

        public event Action OnRecordingStarted;
        public event Action<AudioClip> OnRecordingCompleted;
        public event Action<string> OnRecordingError;

        public AudioInputController(int maxRecordingDuration = 30, int sampleRate = 44100, float silenceThreshold = 0.01f)
        {
            _maxRecordingDuration = maxRecordingDuration;
            _sampleRate = sampleRate;
            _silenceThreshold = silenceThreshold;

            InitializeMicrophone();
        }

        /// <summary>
        /// Initialize microphone device.
        /// </summary>
        private void InitializeMicrophone()
        {
            if (Microphone.devices.Length > 0)
            {
                _microphoneDevice = Microphone.devices[0];
                Debug.Log($"[AudioInputController] Initialized microphone: {_microphoneDevice}");
            }
            else
            {
                Debug.LogError("[AudioInputController] No microphone devices found!");
            }
        }

        /// <summary>
        /// Start recording audio from the microphone.
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording)
            {
                Debug.LogWarning("[AudioInputController] Already recording");
                return;
            }

            if (string.IsNullOrEmpty(_microphoneDevice))
            {
                OnRecordingError?.Invoke("No microphone device available");
                return;
            }

            try
            {
                _recordingClip = Microphone.Start(_microphoneDevice, false, _maxRecordingDuration, _sampleRate);
                _isRecording = true;
                OnRecordingStarted?.Invoke();
                Debug.Log($"[AudioInputController] Recording started (max {_maxRecordingDuration}s)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AudioInputController] Failed to start recording: {ex.Message}");
                OnRecordingError?.Invoke($"Failed to start recording: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop recording and return the recorded audio clip.
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording)
            {
                Debug.LogWarning("[AudioInputController] Not currently recording");
                return;
            }

            try
            {
                int position = Microphone.GetPosition(_microphoneDevice);
                Microphone.End(_microphoneDevice);
                _isRecording = false;

                // Check if we recorded enough audio
                if (position < 1000)
                {
                    Debug.LogWarning($"[AudioInputController] Recording too short: {position} samples");
                    OnRecordingError?.Invoke("Recording too short");
                    return;
                }

                // Trim the recording to actual length
                AudioClip trimmedClip = TrimAudioClip(_recordingClip, position);
                
                Debug.Log($"[AudioInputController] Recording stopped. Duration: {trimmedClip.length:F2}s");
                OnRecordingCompleted?.Invoke(trimmedClip);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AudioInputController] Failed to stop recording: {ex.Message}");
                OnRecordingError?.Invoke($"Failed to stop recording: {ex.Message}");
                _isRecording = false;
            } 
        }

        /// <summary>
        /// Toggle recording state (start if stopped, stop if recording).
        /// </summary>
        public void ToggleRecording()
        {
            if (_isRecording)
                StopRecording();
            else
                StartRecording();
        }

        /// <summary>
        /// Cancel recording without returning audio.
        /// </summary>
        public void CancelRecording()
        {
            if (_isRecording)
            {
                Microphone.End(_microphoneDevice);
                _isRecording = false;
                Debug.Log("[AudioInputController] Recording cancelled");
            }
        }

        /// <summary>
        /// Trim audio clip to actual recorded length, removing unused buffer.
        /// </summary>
        private AudioClip TrimAudioClip(AudioClip clip, int samples)
        {
            if (clip == null)
                return null;

            var soundData = new float[samples * clip.channels];
            clip.GetData(soundData, 0);
            
            var trimmedClip = AudioClip.Create(
                $"{clip.name}_trimmed",
                samples,
                clip.channels,
                clip.frequency,
                false
            );
            
            trimmedClip.SetData(soundData, 0);
            return trimmedClip;
        }

        /// <summary>
        /// Get available microphone devices.
        /// </summary>
        public string[] GetAvailableMicrophones()
        {
            return Microphone.devices;
        }

        /// <summary>
        /// Set the microphone device to use.
        /// </summary>
        public void SetMicrophoneDevice(string deviceName)
        {
            if (_isRecording)
            {
                Debug.LogWarning("[AudioInputController] Cannot change microphone while recording");
                return;
            }

            _microphoneDevice = deviceName;
            Debug.Log($"[AudioInputController] Microphone device set to: {deviceName}");
        }
    }
}
