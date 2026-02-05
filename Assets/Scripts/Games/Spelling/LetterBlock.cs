using UnityEngine;
using TMPro;

namespace LanguageTutor.Games.Spelling
{
    /// <summary>
    /// Represents a single interactive letter block in the spelling game.
    /// Manages the visual text and identity of the letter.
    /// </summary>
    public class LetterBlock : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("The character displayed on this block.")]
        public string letter = "A";

        [Header("References")]
        [SerializeField] private TextMeshPro textComponent;

        private void OnValidate()
        {
            // Automatically updates the visible text when you change the 'letter' variable in the Inspector
            UpdateVisuals();
        }

        public void SetLetter(string newLetter)
        {
            letter = newLetter;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (textComponent != null)
            {
                textComponent.text = letter;
            }
        }
    }
}