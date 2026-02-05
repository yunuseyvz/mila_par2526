using UnityEngine;

namespace LanguageTutor.Games.Spelling
{
    /// <summary>
    /// Represents a target slot where a player must place a letter block.
    /// Handles collision detection and validates if the placed letter is correct.
    /// </summary>
    public class LetterSlot : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("The specific character required for this slot (e.g., 'A').")]
        public string requiredLetter;

        [Header("State")]
        [Tooltip("Indicates whether the correct letter is currently placed in this slot.")]
        public bool isFilled = false;

        /// <summary>
        /// Triggered when another object enters this slot's collider.
        /// </summary>
        /// <param name="other">The collider of the entering object.</param>
        private void OnTriggerEnter(Collider other)
        {
            // Attempt to retrieve the LetterBlock component from the entering object
            // We search in parent because the collider might be on a child or the rigid body might be separate
            var letterBlock = other.GetComponentInParent<LetterBlock>();

            if (letterBlock != null)
            {
                ValidateLetter(letterBlock);
            }
        }

        /// <summary>
        /// Checks if the detected letter matches the requirement.
        /// </summary>
        private void ValidateLetter(LetterBlock block)
        {
            if (isFilled) return;

            // Normalize strings to ensure case-insensitive comparison
            if (block.letter.ToUpper() == requiredLetter.ToUpper())
            {
                Debug.Log($"[LetterSlot] Correct letter '{block.letter}' placed!");
                isFilled = true;

                // Optional: Snap the object to the center of the slot
                SnapObjectToSlot(block.transform);

                // Disable physics to lock it in place (optional game design choice)
                var rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }
            }
            else
            {
                Debug.Log($"[LetterSlot] Incorrect letter. Expected '{requiredLetter}', got '{block.letter}'.");
            }
        }

        private void SnapObjectToSlot(Transform target)
        {
            target.position = this.transform.position;
            target.rotation = this.transform.rotation;
        }
    }
}