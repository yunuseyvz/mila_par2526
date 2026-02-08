using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LanguageTutor.Core;
using LanguageTutor.Data;
using LanguageTutor.Actions;
using TMPro;

namespace LanguageTutor.UI
{
    /// <summary>
    /// Manages the Word Cloud game UI.
    /// Spawns grabbable word bubbles above the tutor, validates ordering,
    /// and provides next-sentence / stop controls after a correct answer.
    /// </summary>
    public class WordReorderingUI : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // Inspector references
        // ──────────────────────────────────────────────

        [Header("Bubble Spawning")]
        [SerializeField] private Transform wordBubbleContainer;
        [SerializeField] private GameObject wordBubblePrefab;
        [SerializeField] private Transform npcHeadTransform; // Position cloud above the tutor's head

        [Header("Layout")]
        [Tooltip("Horizontal spacing between bubble centres")]
        [SerializeField] private float bubbleSpacing = 0.35f;
        [Tooltip("Vertical offset above npcHeadTransform")]
        [SerializeField] private float verticalOffset = 0.5f;
        [Tooltip("Maximum bubbles per row before wrapping")]
        [SerializeField] private int maxBubblesPerRow = 5;
        [Tooltip("Vertical spacing between rows when wrapping")]
        [SerializeField] private float rowSpacing = 0.3f;

        [Header("Animation")]
        [SerializeField] private float spawnDuration = 0.4f;
        [SerializeField] private AnimationCurve spawnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float staggerDelay = 0.08f; // seconds between each bubble's spawn

        [Header("Validation Feedback")]
        [SerializeField] private TextMeshProUGUI feedbackTMP;   // Preferred
        [SerializeField] private Text feedbackTextLegacy;       // Legacy fallback
        [SerializeField] private Color successColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color errorColor   = new Color(1f, 0.3f, 0.2f);
        [SerializeField] private Color infoColor    = Color.white;

        [Header("Post-Round Controls")]
        [Tooltip("Panel shown after the user solves a sentence (contains Next / Stop buttons)")]
        [SerializeField] private GameObject postRoundPanel;
        [SerializeField] private Button nextSentenceButton;
        [SerializeField] private Button stopButton;

        // ──────────────────────────────────────────────
        // Events
        // ──────────────────────────────────────────────

        /// <summary>Fired when the player correctly reorders the sentence.</summary>
        public event Action<string> OnSentenceSolved;

        /// <summary>Fired when the player requests another sentence.</summary>
        public event Action OnNextSentenceRequested;

        /// <summary>Fired when the player wants to stop the game.</summary>
        public event Action OnStopRequested;

        // ──────────────────────────────────────────────
        // Runtime state
        // ──────────────────────────────────────────────

        private readonly List<WordBubble> _currentBubbles = new List<WordBubble>();
        private WordReorderingAction _currentAction;
        private bool _isGameActive;
        private int _roundNumber;

        // ──────────────────────────────────────────────
        // Unity lifecycle
        // ──────────────────────────────────────────────

        private void Start()
        {
            if (wordBubbleContainer == null)
            {
                Debug.LogError("[WordReorderingUI] wordBubbleContainer not assigned!");
                return;
            }

            // Wire up post-round buttons
            if (nextSentenceButton != null)
                nextSentenceButton.onClick.AddListener(HandleNextSentence);
            if (stopButton != null)
                stopButton.onClick.AddListener(HandleStop);

            // Initially hide post-round panel
            SetPostRoundPanelVisible(false);
        }

        private void OnDestroy()
        {
            ClearBubbles();

            if (nextSentenceButton != null)
                nextSentenceButton.onClick.RemoveListener(HandleNextSentence);
            if (stopButton != null)
                stopButton.onClick.RemoveListener(HandleStop);
        }

        // ──────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Initialize (or reset) the word cloud game with a new WordReorderingAction.
        /// Spawns scrambled word bubbles above the tutor.
        /// </summary>
        public void InitializeWordReordering(WordReorderingAction action)
        {
            if (action == null)
            {
                Debug.LogError("[WordReorderingUI] Received null WordReorderingAction!");
                return;
            }

            _currentAction = action;
            _roundNumber++;

            ClearBubbles();
            SetPostRoundPanelVisible(false);

            if (_currentAction.ScrambledWords == null || _currentAction.ScrambledWords.Length == 0)
            {
                Debug.LogError("[WordReorderingUI] No scrambled words to display!");
                return;
            }

            CreateWordBubbles(_currentAction.ScrambledWords);
            _isGameActive = true;

            ShowFeedback($"Round {_roundNumber}: Drag the words into the correct order!", infoColor);
            Debug.Log($"[WordReorderingUI] Round {_roundNumber} started — {_currentAction.ScrambledWords.Length} words");
        }

        /// <summary>
        /// Manually validate the current bubble ordering.
        /// Also called automatically when a bubble is released.
        /// </summary>
        public void CheckWordOrder()
        {
            if (!_isGameActive || _currentAction == null) return;

            // Sort bubbles by world X position (left → right)
            List<WordBubble> sortedBubbles = _currentBubbles
                .OrderBy(b => b.transform.position.x)
                .ToList();

            string[] currentOrder = sortedBubbles.Select(b => b.Word).ToArray();

            if (_currentAction.IsCorrectOrder(currentOrder))
            {
                OnSuccess();
            }
            else
            {
                string currentSentence = string.Join(" ", currentOrder);
                ShowFeedback($"\"{currentSentence}\" — not quite, keep trying!", errorColor);

                // Brief red flash on all bubbles
                StartCoroutine(FlashBubblesError());
            }
        }

        /// <summary>
        /// Remove all bubbles and reset state.
        /// </summary>
        public void ClearBubbles()
        {
            foreach (WordBubble bubble in _currentBubbles)
            {
                if (bubble != null)
                    Destroy(bubble.gameObject);
            }
            _currentBubbles.Clear();
            _isGameActive = false;
        }

        /// <summary>
        /// Fully reset the game (round counter, UI, etc.).
        /// </summary>
        public void ResetGame()
        {
            ClearBubbles();
            _currentAction = null;
            _roundNumber = 0;
            SetPostRoundPanelVisible(false);
            ShowFeedback("", infoColor);
        }

        // ──────────────────────────────────────────────
        // Bubble Creation
        // ──────────────────────────────────────────────

        private void CreateWordBubbles(string[] words)
        {
            Transform container = wordBubbleContainer;

            // Position the container above the NPC head
            if (npcHeadTransform != null)
            {
                container.position = npcHeadTransform.position + Vector3.up * verticalOffset;

                // Face the container toward the camera so bubbles are readable
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    Vector3 lookDir = mainCam.transform.position - container.position;
                    lookDir.y = 0; // Keep level
                    if (lookDir.sqrMagnitude > 0.001f)
                        container.rotation = Quaternion.LookRotation(-lookDir, Vector3.up);
                }
            }

            // Calculate row-based layout
            int totalWords = words.Length;
            int rows = Mathf.CeilToInt((float)totalWords / maxBubblesPerRow);
            int wordIndex = 0;

            for (int row = 0; row < rows; row++)
            {
                int wordsInThisRow = Mathf.Min(maxBubblesPerRow, totalWords - wordIndex);
                float rowStartX = -(wordsInThisRow - 1) * bubbleSpacing / 2f;
                float rowY = (rows - 1 - row) * rowSpacing; // top row first

                for (int col = 0; col < wordsInThisRow; col++)
                {
                    if (wordIndex >= totalWords) break;

                    GameObject bubbleGO = Instantiate(wordBubblePrefab, container);
                    WordBubble bubble = bubbleGO.GetComponent<WordBubble>();

                    if (bubble == null)
                    {
                        Debug.LogError("[WordReorderingUI] wordBubblePrefab missing WordBubble component!");
                        Destroy(bubbleGO);
                        wordIndex++;
                        continue;
                    }

                    // Local position within the container
                    Vector3 localPos = new Vector3(
                        rowStartX + col * bubbleSpacing,
                        rowY,
                        0f);

                    bubbleGO.transform.localPosition = localPos;
                    bubbleGO.transform.localRotation = Quaternion.identity;

                    // Initialize with both grab and release callbacks
                    bubble.Initialize(words[wordIndex], wordIndex,
                        onGrabbed: OnBubbleGrabbed,
                        onReleased: OnBubbleReleased);

                    _currentBubbles.Add(bubble);

                    // Staggered spawn animation
                    StartCoroutine(AnimateBubbleSpawn(bubble, spawnDuration, wordIndex * staggerDelay));

                    wordIndex++;
                }
            }

            Debug.Log($"[WordReorderingUI] Created {words.Length} word bubbles in {rows} row(s)");
        }

        // ──────────────────────────────────────────────
        // Interaction Callbacks
        // ──────────────────────────────────────────────

        private void OnBubbleGrabbed(WordBubble bubble)
        {
            Debug.Log($"[WordReorderingUI] Bubble grabbed: {bubble.Word}");
        }

        private void OnBubbleReleased(WordBubble bubble)
        {
            Debug.Log($"[WordReorderingUI] Bubble released: {bubble.Word}");

            // Auto-check order every time the user releases a bubble
            if (_isGameActive)
                CheckWordOrder();
        }

        // ──────────────────────────────────────────────
        // Game Flow
        // ──────────────────────────────────────────────

        private void OnSuccess()
        {
            _isGameActive = false;

            // Highlight all bubbles green
            foreach (WordBubble bubble in _currentBubbles)
                bubble.SetSuccessColor();

            string correctSentence = _currentAction.CurrentSentence;
            ShowFeedback($"Correct!  \"{correctSentence}\"", successColor);
            Debug.Log($"[WordReorderingUI] Round {_roundNumber} solved!");

            OnSentenceSolved?.Invoke(correctSentence);

            // Show next/stop controls
            SetPostRoundPanelVisible(true);
        }

        private void HandleNextSentence()
        {
            SetPostRoundPanelVisible(false);
            ClearBubbles();
            OnNextSentenceRequested?.Invoke();
        }

        private void HandleStop()
        {
            SetPostRoundPanelVisible(false);
            ClearBubbles();
            ShowFeedback($"Game over — you completed {_roundNumber} round(s)!", infoColor);
            _roundNumber = 0;
            OnStopRequested?.Invoke();
        }

        // ──────────────────────────────────────────────
        // UI Helpers
        // ──────────────────────────────────────────────

        private void SetPostRoundPanelVisible(bool visible)
        {
            if (postRoundPanel != null)
                postRoundPanel.SetActive(visible);
        }

        private void ShowFeedback(string message, Color color)
        {
            if (feedbackTMP != null)
            {
                feedbackTMP.text = message;
                feedbackTMP.color = color;
            }
            else if (feedbackTextLegacy != null)
            {
                feedbackTextLegacy.text = message;
                feedbackTextLegacy.color = color;
            }
        }

        // ──────────────────────────────────────────────
        // Animations / Coroutines
        // ──────────────────────────────────────────────

        private IEnumerator AnimateBubbleSpawn(WordBubble bubble, float duration, float delay)
        {
            Vector3 targetScale = bubble.transform.localScale;
            bubble.transform.localScale = Vector3.zero;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = spawnCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
                bubble.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, t);
                yield return null;
            }
            bubble.transform.localScale = targetScale;
        }

        private IEnumerator FlashBubblesError()
        {
            foreach (WordBubble b in _currentBubbles) b.SetErrorColor();
            yield return new WaitForSeconds(0.4f);
            foreach (WordBubble b in _currentBubbles) b.ResetColor();
        }
    }
}
