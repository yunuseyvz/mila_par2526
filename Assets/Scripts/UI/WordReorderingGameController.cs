using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using LanguageTutor.Core;
using LanguageTutor.Data;
using LanguageTutor.Actions;

namespace LanguageTutor.UI
{
    /// <summary>
    /// Orchestrates the Word Cloud game mode.
    /// Bridges NPCController (LLM calls) ↔ WordReorderingUI (bubble display).
    /// Handles the "next sentence / stop" loop after the user solves a round.
    /// </summary>
    public class WordReorderingGameController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NPCController npcController;
        [SerializeField] private WordReorderingUI wordReorderingUI;
        [SerializeField] private Button submitButton;

        [Header("Auto-Generate Settings")]
        [Tooltip("If true, automatically asks the LLM for a new sentence when the user presses 'Next'.")]
        [SerializeField] private bool autoGenerateOnNext = true;
        [Tooltip("User prompt sent to the LLM when auto-generating a new round.")]
        [SerializeField] private string autoGeneratePrompt = "Give me another sentence to practice.";

        private WordReorderingAction _currentWordAction;
        private bool _isGenerating;

        // ──────────────────────────────────────────────
        // Unity lifecycle
        // ──────────────────────────────────────────────

        private void Start()
        {
            if (npcController == null)
                npcController = FindObjectOfType<NPCController>();
            if (wordReorderingUI == null)
                wordReorderingUI = FindObjectOfType<WordReorderingUI>();

            // Submit button (manual check)
            if (submitButton != null)
                submitButton.onClick.AddListener(OnSubmitClicked);

            // Listen to UI events
            if (wordReorderingUI != null)
            {
                wordReorderingUI.OnNextSentenceRequested += HandleNextSentenceRequested;
                wordReorderingUI.OnStopRequested += HandleStopRequested;
                wordReorderingUI.OnSentenceSolved += HandleSentenceSolved;
            }
        }

        private void OnDestroy()
        {
            if (submitButton != null)
                submitButton.onClick.RemoveListener(OnSubmitClicked);

            if (wordReorderingUI != null)
            {
                wordReorderingUI.OnNextSentenceRequested -= HandleNextSentenceRequested;
                wordReorderingUI.OnStopRequested -= HandleStopRequested;
                wordReorderingUI.OnSentenceSolved -= HandleSentenceSolved;
            }
        }

        // ──────────────────────────────────────────────
        // Public API — called by NPCController
        // ──────────────────────────────────────────────

        /// <summary>
        /// Display a new word reordering round from an already-executed WordReorderingAction.
        /// Called by NPCController.HandleLLMResponseReceived when in WordReordering mode.
        /// </summary>
        public void DisplayWordReorderingGame(WordReorderingAction wordAction)
        {
            if (wordAction == null)
            {
                Debug.LogError("[WordReorderingGameController] Received null WordReorderingAction!");
                return;
            }

            _currentWordAction = wordAction;

            Debug.Log($"[WordReorderingGameController] Displaying round");
            Debug.Log($"  Correct sentence : {wordAction.CurrentSentence}");
            Debug.Log($"  Scrambled display : {string.Join(" ", wordAction.ScrambledWords)}");

            if (wordReorderingUI != null)
                wordReorderingUI.InitializeWordReordering(wordAction);
        }

        /// <summary>
        /// Clear everything and reset the game.
        /// </summary>
        public void ResetWordReordering()
        {
            if (wordReorderingUI != null)
                wordReorderingUI.ResetGame();

            _currentWordAction = null;
            _isGenerating = false;
        }

        // ──────────────────────────────────────────────
        // UI interaction handlers
        // ──────────────────────────────────────────────

        private void OnSubmitClicked()
        {
            wordReorderingUI?.CheckWordOrder();
        }

        private void HandleSentenceSolved(string correctSentence)
        {
            Debug.Log($"[WordReorderingGameController] Round solved: \"{correctSentence}\"");
            // The UI already shows next/stop panel — nothing else to do here.
            // You could trigger TTS of the correct sentence, give XP, etc.
        }

        private async void HandleNextSentenceRequested()
        {
            Debug.Log("[WordReorderingGameController] Next sentence requested");

            if (!autoGenerateOnNext || npcController == null)
            {
                Debug.LogWarning("[WordReorderingGameController] Cannot auto-generate — missing references or disabled");
                return;
            }

            await GenerateNewRoundAsync();
        }

        private void HandleStopRequested()
        {
            Debug.Log("[WordReorderingGameController] Stop requested — returning to idle");
            ResetWordReordering();
            // Optionally switch back to Chat mode:
            // npcController?.SetActionMode(ActionMode.Chat);
        }

        // ──────────────────────────────────────────────
        // LLM integration for auto-generated rounds
        // ──────────────────────────────────────────────

        /// <summary>
        /// Ask the LLM for a new sentence via NPCController and display it as a new round.
        /// </summary>
        private async Task GenerateNewRoundAsync()
        {
            if (_isGenerating) return;
            _isGenerating = true;

            try
            {
                Debug.Log("[WordReorderingGameController] Generating new sentence via LLM...");

                // Create a fresh action and context
                WordReorderingAction newAction = npcController.CreateWordReorderingAction();
                if (newAction == null)
                {
                    Debug.LogError("[WordReorderingGameController] Failed to create WordReorderingAction from NPCController");
                    return;
                }

                // Execute through NPCController's pipeline
                bool success = await npcController.ExecuteWordReorderingRoundAsync(newAction, autoGeneratePrompt);
                
                if (success)
                {
                    DisplayWordReorderingGame(newAction);
                }
                else
                {
                    Debug.LogError("[WordReorderingGameController] LLM failed to generate a new sentence");
                }
            }
            finally
            {
                _isGenerating = false;
            }
        }
    }
}
