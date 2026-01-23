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
        
        [Header("Settings")]
        [SerializeField] private float slowModeSpeed = 0.75f;
        [SerializeField] private float normalSpeed = 1.0f;

        private void Start()
        {
            if (npcController == null)
            {
                npcController = FindObjectOfType<NPCController>();
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
    }
}
