using UnityEngine;
using UnityEngine.UI;
using LanguageTutor.Core;

namespace LanguageTutor.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NPCController npcController;
        [SerializeField] private Toggle slowModeToggle;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button resetButton;
        
        [Header("Settings")]
        [SerializeField] private float slowModeSpeed = 0.75f;
        [SerializeField] private float normalSpeed = 1.0f;

        private void Start()
        {
            if (npcController == null)
            {
                npcController = FindObjectOfType<NPCController>();
            }

            if (stopButton == null)
            {
                stopButton = FindButtonByName("StopButton") ?? FindButtonByName("SettingsButton");
            }

            if (resetButton == null)
            {
                resetButton = FindButtonByName("ResetButton") ?? FindButtonByName("Reset");
            }

            if (slowModeToggle != null)
            {
                slowModeToggle.onValueChanged.AddListener(OnSlowModeChanged);
                
                // Initialize state
                OnSlowModeChanged(slowModeToggle.isOn);
            }
            else
            {
                Debug.LogWarning("[MainMenuController] Slow Mode Toggle is not assigned!");
            }

            if (replayButton != null)
            {
                replayButton.onClick.AddListener(OnReplayClicked);
            }
            else
            {
                Debug.LogWarning("[MainMenuController] Replay Button is not assigned!");
            }

            if (stopButton != null)
            {
                stopButton.onClick.AddListener(OnStopClicked);
            }
            else
            {
                Debug.LogWarning("[MainMenuController] Stop Button is not assigned!");
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetClicked);
            }
            else
            {
                Debug.LogWarning("[MainMenuController] Reset Button is not assigned!");
            }
        }

        private void OnDestroy()
        {
            if (slowModeToggle != null)
            {
                slowModeToggle.onValueChanged.RemoveListener(OnSlowModeChanged);
            }

            if (replayButton != null)
            {
                replayButton.onClick.RemoveListener(OnReplayClicked);
            }

            if (stopButton != null)
            {
                stopButton.onClick.RemoveListener(OnStopClicked);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(OnResetClicked);
            }
        }

        public void OnSlowModeChanged(bool isSlow)
        {
            if (npcController != null)
            {
                float targetSpeed = isSlow ? slowModeSpeed : normalSpeed;
                Debug.Log($"[MainMenuController] Setting TTS speed to {targetSpeed} (Slow mode: {isSlow})");
                npcController.SetTTSSpeed(targetSpeed);
            }
            else
            {
                Debug.LogError("[MainMenuController] NPCController not found!");
            }
        }

        private void OnReplayClicked()
        {
            if (npcController != null)
            {
                Debug.Log("[MainMenuController] Replay button clicked");
                npcController.ReplayLastMessage();
            }
        }

        private void OnStopClicked()
        {
            if (npcController != null)
            {
                Debug.Log("[MainMenuController] Stop button clicked");
                npcController.StopCurrentSpeech();
            }
        }

        private void OnResetClicked()
        {
            if (npcController != null)
            {
                Debug.Log("[MainMenuController] Reset button clicked");
                npcController.ReinitializeConversationPipeline();
            }
        }

        private static Button FindButtonByName(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<Button>();
        }
    }
}
