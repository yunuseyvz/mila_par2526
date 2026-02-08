using UnityEngine;
using System;
using TMPro;

namespace LanguageTutor.UI
{
    /// <summary>
    /// Individual word bubble component for the Word Cloud game.
    /// Grabbable and draggable in VR — users rearrange these to form the correct sentence.
    /// Attach to a prefab that has: TextMeshPro, Renderer, Rigidbody, Collider,
    /// and (optionally) an XRGrabInteractable for VR hand-tracking / controller grab.
    /// </summary>
    public class WordBubble : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TextMeshPro wordText;
        [SerializeField] private Renderer bubbleRenderer;
        [SerializeField] private Color normalColor = new Color(0.2f, 0.6f, 1f, 0.85f);
        [SerializeField] private Color grabColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color successColor = new Color(0.2f, 0.9f, 0.3f, 1f);
        [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.2f, 1f);

        [Header("Physics")]
        [SerializeField] private Rigidbody rigidBody;
        [SerializeField] private bool usePhysics = true;
        [SerializeField] private float snapDamping = 10f;

        [Header("Scale Animation")]
        [SerializeField] private float hoverScaleMultiplier = 1.15f;
        [SerializeField] private float scaleAnimSpeed = 8f;

        /// <summary>The word this bubble displays.</summary>
        public string Word { get; private set; }

        /// <summary>Original scrambled index (for identification).</summary>
        public int Index { get; private set; }

        /// <summary>Whether the bubble is currently grabbed by the user.</summary>
        public bool IsGrabbed => _isGrabbed;

        // Callbacks
        private Action<WordBubble> _onGrabbed;
        private Action<WordBubble> _onReleased;
        private bool _isGrabbed;
        private Vector3 _baseScale;
        private Vector3 _targetScale;

        // ──────────────────────────────────────────────
        // Initialization
        // ──────────────────────────────────────────────

        /// <summary>
        /// Initialize the bubble with its word, index, and optional callbacks.
        /// </summary>
        public void Initialize(string word, int index,
            Action<WordBubble> onGrabbed = null,
            Action<WordBubble> onReleased = null)
        {
            Word = word;
            Index = index;
            _onGrabbed = onGrabbed;
            _onReleased = onReleased;

            if (wordText != null)
                wordText.text = word;

            // Auto-size bubble width based on word length
            AdjustBubbleWidth();

            if (!usePhysics && rigidBody != null)
                rigidBody.isKinematic = true;

            _baseScale = transform.localScale;
            _targetScale = _baseScale;

            SetColor(normalColor);
        }

        /// <summary>
        /// Overload that accepts the legacy (WordBubble, Vector3) callback signature.
        /// </summary>
        public void Initialize(string word, int index,
            Action<WordBubble, Vector3> onGrabbed)
        {
            Initialize(word, index,
                onGrabbed: b => onGrabbed?.Invoke(b, b.transform.position),
                onReleased: null);
        }

        // ──────────────────────────────────────────────
        // VR Interaction Hooks
        // ──────────────────────────────────────────────

        /// <summary>
        /// Called when user grabs the bubble.
        /// Hook this to XRGrabInteractable's Select Entered event in the Inspector.
        /// </summary>
        public void OnGrab()
        {
            _isGrabbed = true;
            _targetScale = _baseScale * hoverScaleMultiplier;
            SetColor(grabColor);
            _onGrabbed?.Invoke(this);
        }

        /// <summary>
        /// Called when user releases the bubble.
        /// Hook this to XRGrabInteractable's Select Exited event in the Inspector.
        /// </summary>
        public void OnRelease()
        {
            _isGrabbed = false;
            _targetScale = _baseScale;
            SetColor(normalColor);
            _onReleased?.Invoke(this);
        }

        // ──────────────────────────────────────────────
        // Visual Helpers
        // ──────────────────────────────────────────────

        /// <summary>Set an arbitrary color on the bubble renderer.</summary>
        public void SetColor(Color color)
        {
            if (bubbleRenderer != null)
            {
                // Use MaterialPropertyBlock to avoid material instance leaks
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                bubbleRenderer.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                bubbleRenderer.SetPropertyBlock(block);
            }
        }

        /// <summary>Mark bubble as correct (green).</summary>
        public void SetSuccessColor() => SetColor(successColor);

        /// <summary>Mark bubble as incorrect (red flash).</summary>
        public void SetErrorColor() => SetColor(errorColor);

        /// <summary>Reset bubble to its default color.</summary>
        public void ResetColor() => SetColor(normalColor);

        /// <summary>Current world position (used for left-to-right ordering check).</summary>
        public Vector3 GetPosition() => transform.position;

        // ──────────────────────────────────────────────
        // Private Helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Optionally widen the bubble background based on character count
        /// so long words don't overflow.
        /// </summary>
        private void AdjustBubbleWidth()
        {
            if (bubbleRenderer == null || string.IsNullOrEmpty(Word)) return;

            // Minimum width + extra per character beyond 3 chars
            float baseWidth = 1.0f;
            float extraPerChar = 0.12f;
            int extraChars = Mathf.Max(0, Word.Length - 3);

            Vector3 localScale = bubbleRenderer.transform.localScale;
            localScale.x = baseWidth + extraChars * extraPerChar;
            bubbleRenderer.transform.localScale = localScale;
        }

        private void Update()
        {
            // Smooth scale animation
            if (transform.localScale != _targetScale)
            {
                transform.localScale = Vector3.Lerp(
                    transform.localScale, _targetScale,
                    Time.deltaTime * scaleAnimSpeed);
            }
        }

        public override string ToString()
        {
            return $"WordBubble(\"{Word}\", idx={Index}, grabbed={_isGrabbed})";
        }
    }
}
