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
        [Tooltip("Indicates whether the CORRECT letter is currently snapped in this slot.")]
        public bool isFilled = false;

        [Tooltip("Indicates whether ANY letter block is currently snapped in this slot.")]
        public bool isOccupied => currentBlock != null;

        private LetterBlock currentBlock = null;

        private void Start()
        {
            // Robustness: Ensure the collider is a trigger and large enough to catch blocks easily
            // The slot scale is usually (0.14, 0.02, 0.14).
            // Reduced multiplier to 10f based on user feedback (was too big).
            // 0.02 * 10 = 0.2m height (20cm capture zone).
            var col = GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();

            col.isTrigger = true;
            
            // X/Z = 1.0f -> Keep tight to width
            // Y = 10f -> ~0.2m height
            col.size = new Vector3(1.0f, 10f, 1.0f);
        }

        private void Update()
        {
            // Continuous "Magnetic" Pull
            if (currentBlock != null)
            {
                // Target Position: Slot Center + Vertical Offset
                // Block Scale ~0.12, Slot Box Y ~0.02.
                // We want it to sit *on top*. 
                // Slot Center is at Y=0 (relative). Top is +0.01.
                // Block Center is at Y=0 (relative). Bottom is -0.06.
                // So we need to raise it by 0.01 + 0.06 + extra gap? ~0.08f total seems safe.
                Vector3 targetPos = transform.position + (transform.up * 0.08f);

                // Smoothly snap
                float snapSpeed = Time.deltaTime * 15f; // Slightly softer
                currentBlock.transform.position = Vector3.Lerp(currentBlock.transform.position, targetPos, snapSpeed);
                currentBlock.transform.rotation = Quaternion.Lerp(currentBlock.transform.rotation, transform.rotation, snapSpeed);

                // Enforce physics silence
                var rb = currentBlock.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    if (!rb.isKinematic) 
                    {
                         rb.linearVelocity = Vector3.zero;
                         rb.angularVelocity = Vector3.zero;
                    }
                }
            }
        }

        /// <summary>
        /// Triggered when another object enters this slot's collider.
        /// </summary>
        /// <param name="other">The collider of the entering object.</param>
        private void OnTriggerEnter(Collider other)
        {
            // If already occupied by a different block, ignore
            if (isOccupied) return;

            var letterBlock = other.GetComponentInParent<LetterBlock>();
            if (letterBlock != null)
            {
                Debug.Log($"[LetterSlot] Captured {letterBlock.letter}. Component: {other.name}");
                
                // 1. Occupy
                currentBlock = letterBlock;

                // 2. Validate (Logic)
                ValidateLetter(letterBlock);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var letterBlock = other.GetComponentInParent<LetterBlock>();
            
            // If the exiting block is ours
            if (letterBlock != null && letterBlock == currentBlock)
            {
                Debug.Log($"[LetterSlot] Released {letterBlock.letter}.");
                
                // Clear state
                currentBlock = null;
                isFilled = false;

                // Re-enable physics if needed (Optional: The interaction system should handle this usually)
                var rb = letterBlock.GetComponent<Rigidbody>();
                if (rb != null)
                {
                     rb.isKinematic = false;
                     rb.useGravity = false; // Keep gravity off as per game design
                }
            }
        }

        /// <summary>
        /// Checks if the detected letter matches the requirement.
        /// </summary>
        private void ValidateLetter(LetterBlock block)
        {
            // Normalize strings to ensure case-insensitive comparison
            if (block.letter.ToUpper() == requiredLetter.ToUpper())
            {
                Debug.Log($"[LetterSlot] Correct letter '{block.letter}' placed!");
                isFilled = true;
            }
            else
            {
                Debug.Log($"[LetterSlot] Incorrect letter. Expected '{requiredLetter}', got '{block.letter}'.");
                isFilled = false;
            }
        }
    }
}