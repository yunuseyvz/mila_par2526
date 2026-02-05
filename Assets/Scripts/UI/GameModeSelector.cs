using UnityEngine;
using LanguageTutor.Core;
using UnityEngine.UI;

namespace LanguageTutor.UI
{
    /// <summary>
    /// Handles the activation of specific game modes via UI interactions.
    /// Supports both standard Buttons and Toggles (Dynamic Boolean).
    /// </summary>
    public class GameModeSelector : MonoBehaviour
    {
        [Tooltip("The specific action mode this UI element should activate.")]
        public ActionMode targetMode;

        /// <summary>
        /// Activates the selected mode if the toggle state is true.
        /// Binds to the 'On Value Changed (Boolean)' event of a UI Toggle.
        /// </summary>
        /// <param name="isOn">Current state of the toggle (passed dynamically).</param>
        public void ActivateMode(bool isOn)
        {
            // Only proceed if the toggle was switched ON
            if (!isOn) return;

            var controller = FindObjectOfType<NPCController>();

            if (controller != null)
            {
                Debug.Log($"[UI] Toggle active. Switching NPCController mode to: {targetMode}");
                controller.SetActionMode(targetMode);
            }
            else
            {
                Debug.LogError("[UI] Critical Error: No NPCController found in the scene.");
            }
        }
    }
}
