using UnityEngine;
using UnityEngine.UI;
using LanguageTutor.Core;
using LanguageTutor.Data;

namespace LanguageTutor.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NPCController npcController;
        [SerializeField] private Toggle slowModeToggle;
        [SerializeField] private Button replayButton;
        
        [Header("Game Mode Buttons")]
        [SerializeField] private Toggle freeTalkButton;
        [SerializeField] private Toggle objectTaggingButton;
        [SerializeField] private Toggle wordChunksButton;
        [SerializeField] private Toggle roleplayButton;
        
        [Header("Roleplay Scenarios")]
        [Tooltip("Roleplay scenario for Roleplay mode (e.g., Coffee Shop Waiter)")]
        [SerializeField] private RoleplayScenarioConfig roleplayScenario;
        
        [Header("Settings")]
        [SerializeField] private float slowModeSpeed = 0.75f;
        [SerializeField] private float normalSpeed = 1.0f;
        
        // Guard flag to prevent recursive toggle updates
        private bool isUpdatingToggles = false;

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

            // Setup game mode buttons (using Toggle)
            if (freeTalkButton != null)
                freeTalkButton.onValueChanged.AddListener((isOn) => { if (isOn) OnFreeTalkMode(); });
            else
                Debug.LogWarning("[MainMenuController] Free Talk Button not assigned!");
            
            if (objectTaggingButton != null)
                objectTaggingButton.onValueChanged.AddListener((isOn) => { if (isOn) OnObjectTaggingMode(); });
            else
                Debug.LogWarning("[MainMenuController] Object Tagging Button not assigned!");
            
            if (wordChunksButton != null)
            {
                wordChunksButton.onValueChanged.AddListener((isOn) => { if (isOn) OnWordChunksMode(); });
                Debug.Log("[MainMenuController] ✓ Word Chunks Button listener registered");
            }
            else
                Debug.LogError("[MainMenuController] ❌ Word Chunks Button NOT assigned in Inspector!");
            
            if (roleplayButton != null)
                roleplayButton.onValueChanged.AddListener((isOn) => { if (isOn) OnRoleplayMode(); });
            else
                Debug.LogWarning("[MainMenuController] Roleplay Button not assigned!");
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

            // Clean up game mode buttons
            if (freeTalkButton != null)
                freeTalkButton.onValueChanged.RemoveAllListeners();
            
            if (objectTaggingButton != null)
                objectTaggingButton.onValueChanged.RemoveAllListeners();
            
            if (wordChunksButton != null)
                wordChunksButton.onValueChanged.RemoveAllListeners();
            
            if (roleplayButton != null)
                roleplayButton.onValueChanged.RemoveAllListeners();
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

        /// <summary>
        /// Free Talk Mode: General conversation using voice → STT → LLM → TTS
        /// </summary>
        private void OnFreeTalkMode()
        {
            if (isUpdatingToggles) return;
            if (npcController == null) return;
            
            isUpdatingToggles = true;
            
            // Turn off other toggles
            if (objectTaggingButton != null) objectTaggingButton.isOn = false;
            if (wordChunksButton != null) wordChunksButton.isOn = false;
            if (roleplayButton != null) roleplayButton.isOn = false;
            
            // Keep this one on
            if (freeTalkButton != null) freeTalkButton.isOn = true;
            
            isUpdatingToggles = false;
            
            npcController.SetActionMode(ActionMode.Chat);
            Debug.Log("[MainMenuController] 💬 Free Talk Mode activated - General conversation");
        }

        /// <summary>
        /// Object Tagging Mode: Grammar correction practice
        /// </summary>
        private void OnObjectTaggingMode()
        {
            if (isUpdatingToggles) return;
            if (npcController == null) return;
            
            isUpdatingToggles = true;
            
            // Turn off other toggles
            if (freeTalkButton != null) freeTalkButton.isOn = false;
            if (wordChunksButton != null) wordChunksButton.isOn = false;
            if (roleplayButton != null) roleplayButton.isOn = false;
            
            // Keep this one on
            if (objectTaggingButton != null) objectTaggingButton.isOn = true;
            
            isUpdatingToggles = false;
            
            npcController.SetActionMode(ActionMode.ObjectTaggingVision);
            Debug.Log("[MainMenuController] ✏️ Object Tagging Mode activated - Vision object tagging");
        }

        /// <summary>
        /// Word Chunks Mode: Word Reordering - LLM returns a scrambled sentence
        /// Player reorders words using Meta controllers and hands until correct.
        /// </summary>
        private void OnWordChunksMode()
        {
            if (isUpdatingToggles) return;
            if (npcController == null) return;
            
            isUpdatingToggles = true;
            
            // Turn off other toggles
            if (freeTalkButton != null) freeTalkButton.isOn = false;
            if (objectTaggingButton != null) objectTaggingButton.isOn = false;
            if (roleplayButton != null) roleplayButton.isOn = false;
            
            // Keep this one on
            if (wordChunksButton != null) wordChunksButton.isOn = true;
            
            isUpdatingToggles = false;
            
            npcController.SetActionMode(ActionMode.WordReordering);
            Debug.Log("[MainMenuController] 🔤 Word Chunks Mode activated - Reorder scrambled words");
        }

        /// <summary>
        /// Roleplay Mode: Roleplay scenarios (e.g., coffee shop waiter)
        /// Uses the same voice → STT → LLM → TTS pipeline but with roleplay context
        /// </summary>
        private void OnRoleplayMode()
        {
            if (isUpdatingToggles) return;
            if (npcController == null) return;
            
            isUpdatingToggles = true;
            
            // Turn off other toggles
            if (freeTalkButton != null) freeTalkButton.isOn = false;
            if (objectTaggingButton != null) objectTaggingButton.isOn = false;
            if (wordChunksButton != null) wordChunksButton.isOn = false;
            
            // Keep this one on
            if (roleplayButton != null) roleplayButton.isOn = true;
            
            isUpdatingToggles = false;
            
            // If a roleplay scenario is assigned, use it to customize the conversation
            if (roleplayScenario != null)
            {
                npcController.SetRoleplayScenario(roleplayScenario);
                Debug.Log($"[MainMenuController] 🎭 Roleplay Mode activated - Roleplay: {roleplayScenario.scenarioName}");
            }
            else
            {
                // Fallback to default conversation practice
                npcController.SetActionMode(ActionMode.ConversationPractice);
                Debug.Log("[MainMenuController] 🎭 Roleplay Mode activated - Conversation practice");
            }
        }

        /// <summary>
        /// Generic method to set game mode
        /// </summary>
        public void SetGameMode(ActionMode mode)
        {
            if (npcController != null)
            {
                npcController.SetActionMode(mode);
                Debug.Log($"[MainMenuController] Game mode set to: {mode}");
            }
            else
            {
                Debug.LogError("[MainMenuController] NPCController not found!");
            }
        }

        /// <summary>
        /// Public method to activate Word Reordering mode
        /// Can be called directly from UI button or other sources
        /// </summary>
        public void ActivateWordReorderingMode()
        {
            Debug.Log("[MainMenuController] ActivateWordReorderingMode called");
            OnWordChunksMode();
        }
    }
}
