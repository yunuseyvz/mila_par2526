using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using LanguageTutor.Actions;
using LanguageTutor.Services.LLM;

namespace LanguageTutor.Data
{
    /// <summary>
    /// Word Reordering Action: LLM returns a correct sentence, words are scrambled
    /// and displayed as grabbable word cloud bubbles above the tutor.
    /// Player reorders words using Meta controllers until correct sequence is achieved.
    /// </summary>
    public class WordReorderingAction : ILLMAction
    {
        private readonly string _systemPrompt;

        /// <summary>
        /// The original correct sentence from the LLM.
        /// </summary>
        public string CurrentSentence { get; private set; }

        /// <summary>
        /// Words in scrambled order (what the player sees initially).
        /// </summary>
        public string[] ScrambledWords { get; private set; }

        /// <summary>
        /// Words in the correct order (the answer).
        /// </summary>
        public string[] CorrectWords { get; private set; }

        /// <summary>
        /// Number of words in the sentence.
        /// </summary>
        public int WordCount => CorrectWords?.Length ?? 0;

        /// <summary>
        /// Whether this action has been solved by the user.
        /// </summary>
        public bool IsSolved { get; private set; }

        public WordReorderingAction(string systemPrompt = null)
        {
            this._systemPrompt = systemPrompt;
        }

        public string GetActionName() => "Word Reordering";

        public bool CanExecute(LLMActionContext context)
        {
            return !string.IsNullOrWhiteSpace(context?.UserInput);
        }

        public async Task<LLMActionResult> ExecuteAsync(ILLMService llmService, LLMActionContext context)
        {
            try
            {
                IsSolved = false;

                string systemPrompt = !string.IsNullOrEmpty(context.SystemPrompt) 
                    ? context.SystemPrompt 
                    : _systemPrompt;

                string response;
                
                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    response = await llmService.GenerateResponseAsync(
                        context.UserInput, 
                        systemPrompt, 
                        context.ConversationHistory);
                }
                else
                {
                    response = await llmService.GenerateResponseAsync(
                        context.UserInput, 
                        context.ConversationHistory);
                }

                // Process the response to extract and scramble words
                ProcessResponse(response);

                // Return the scrambled version so the subtitle shows the puzzle
                string scrambledText = string.Join(" ", ScrambledWords);
                return LLMActionResult.CreateSuccess(scrambledText);
            }
            catch (Exception ex)
            {
                return LLMActionResult.CreateFailure($"Word Reordering action failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Process LLM response: clean up, split into words, and scramble.
        /// </summary>
        public void ProcessResponse(string llmResponse)
        {
            // Clean up: remove extra whitespace, quotes, numbering
            string cleaned = llmResponse.Trim();
            cleaned = cleaned.Trim('"', '\'', '\u201C', '\u201D'); // Remove surrounding quotes
            cleaned = Regex.Replace(cleaned, @"^\d+\.?\s*", ""); // Remove leading numbering
            cleaned = Regex.Replace(cleaned, @"\s+", " "); // Normalize whitespace
            
            // If the LLM returned multiple lines, take only the first non-empty line
            string[] lines = cleaned.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            cleaned = lines.Length > 0 ? lines[0].Trim() : cleaned;

            // Store the correct sentence
            CurrentSentence = cleaned;
            
            // Split into words (preserving punctuation attached to words)
            CorrectWords = CurrentSentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Scramble the words ensuring they differ from the correct order
            ScrambledWords = (string[])CorrectWords.Clone();
            ShuffleUntilDifferent(ScrambledWords, CorrectWords);
            
            Debug.Log($"[WordReorderingAction] Original: {CurrentSentence}");
            Debug.Log($"[WordReorderingAction] Scrambled: {string.Join(" ", ScrambledWords)}");
        }

        /// <summary>
        /// Check if current word order matches correct order (case-insensitive comparison).
        /// </summary>
        public bool IsCorrectOrder(string[] currentOrder)
        {
            if (currentOrder == null || currentOrder.Length != CorrectWords.Length)
                return false;

            for (int i = 0; i < currentOrder.Length; i++)
            {
                if (!string.Equals(currentOrder[i], CorrectWords[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            IsSolved = true;
            return true;
        }

        /// <summary>
        /// Fisher-Yates shuffle, repeated until the result differs from the original.
        /// </summary>
        private void ShuffleUntilDifferent(string[] array, string[] original)
        {
            // If only 1 word, nothing to shuffle
            if (array.Length <= 1) return;

            int maxAttempts = 20;
            int attempt = 0;

            do
            {
                ShuffleArray(array);
                attempt++;
            }
            while (array.SequenceEqual(original) && attempt < maxAttempts);
        }

        /// <summary>
        /// Fisher-Yates shuffle algorithm.
        /// </summary>
        private void ShuffleArray(string[] array)
        {
            System.Random random = new System.Random();
            for (int i = array.Length - 1; i > 0; i--)
            {
                int randomIndex = random.Next(0, i + 1);
                string temp = array[i];
                array[i] = array[randomIndex];
                array[randomIndex] = temp;
            }
        }

        public override string ToString()
        {
            return $"Word Reordering: {CurrentSentence} (Solved: {IsSolved})";
        }
    }
}

