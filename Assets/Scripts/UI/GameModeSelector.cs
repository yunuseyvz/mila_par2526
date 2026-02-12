using UnityEngine;
using LanguageTutor.Core;
using UnityEngine.UI;
using LanguageTutor.Data;

namespace LanguageTutor.UI
{
    /// <summary>
    /// Handles the activation of specific game modes via UI interactions.
    /// Supports both standard Buttons and Toggles (Dynamic Boolean).
    /// </summary>
    public class GameModeSelector : MonoBehaviour
    {
        [Tooltip("The specific game mode this UI element should activate.")]
        public ConversationGameMode targetMode;

        private NPCController _controller;
        private Animator _animator;

        private static readonly int NormalHash = Animator.StringToHash("Normal");
        private static readonly int HighlightedHash = Animator.StringToHash("Highlighted");
        private static readonly int PressedHash = Animator.StringToHash("Pressed");
        private static readonly int SelectedHash = Animator.StringToHash("Selected");
        private static readonly int DisabledHash = Animator.StringToHash("Disabled");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            NPCController.OnGameModeChanged += HandleGameModeChanged;
            RefreshVisualStateFromController();
        }

        private void OnDisable()
        {
            NPCController.OnGameModeChanged -= HandleGameModeChanged;
        }

        private void HandleGameModeChanged(ConversationGameMode mode)
        {
            ApplyVisualState(mode == targetMode);
        }

        private void RefreshVisualStateFromController()
        {
            if (_controller == null)
                _controller = FindObjectOfType<NPCController>();

            if (_controller != null)
            {
                ApplyVisualState(_controller.CurrentGameMode == targetMode);
            }
        }

        private void ApplyVisualState(bool isActive)
        {
            if (_animator == null)
                return;

            _animator.ResetTrigger(NormalHash);
            _animator.ResetTrigger(HighlightedHash);
            _animator.ResetTrigger(PressedHash);
            _animator.ResetTrigger(SelectedHash);
            _animator.ResetTrigger(DisabledHash);

            _animator.SetTrigger(isActive ? SelectedHash : NormalHash);
        }

        /// <summary>
        /// Activates the selected mode from a Button OnClick event.
        /// </summary>
        public void ActivateMode()
        {
            ActivateMode(true);
        }

        /// <summary>
        /// Activates the selected mode if the toggle state is true.
        /// Binds to the 'On Value Changed (Boolean)' event of a UI Toggle.
        /// </summary>
        /// <param name="isOn">Current state of the toggle (passed dynamically).</param>
        public void ActivateMode(bool isOn)
        {
            // Only proceed if the toggle was switched ON
            if (!isOn) return;

            if (_controller == null)
                _controller = FindObjectOfType<NPCController>();

            var controller = _controller;

            if (controller != null)
            {
                Debug.Log($"[UI] Toggle active. Switching NPCController mode to: {targetMode}");
                controller.SetGameMode(targetMode);
                ApplyVisualState(true);
            }
            else
            {
                Debug.LogError("[UI] Critical Error: No NPCController found in the scene.");
            }
        }
    }
}
