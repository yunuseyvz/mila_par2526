using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using LanguageTutor.Services.LLM;
using LanguageTutor.Core;
using LanguageTutor.Games.Spelling;
using System.Text.RegularExpressions;

namespace LanguageTutor.Actions
{
    public class SpellingGameAction : ILLMAction
    {
        // --- CONFIGURATION ---
        private const float SLOT_SPACING = 0.25f;
        private const float BLOCK_SPACING = 0.20f;
        private const float GAME_HEIGHT = 1.0f;
        private const float SLOT_DISTANCE = 0.7f;
        private const float BLOCK_DISTANCE = 0.4f;
        private const int EXTRA_LETTERS_COUNT = 3;
        private const float BLOCK_SCALE = 0.15f;
        private readonly Vector3 SLOT_SCALE = new Vector3(0.18f, 0.02f, 0.18f);

        // --- STATE ---
        private string targetWord = "";

        // Lists to track objects so we can delete them later
        private List<LetterSlot> activeSlots = new List<LetterSlot>();
        private List<GameObject> spawnedBlocks = new List<GameObject>(); // NEW: Track blocks

        private bool isGameRunning = false;

        public string GetActionName() => "SpellingGame";
        public bool CanExecute(LLMActionContext context) => true;

        public async Task<LLMActionResult> ExecuteAsync(ILLMService llmService, LLMActionContext context)
        {
            // Reset lists just in case
            activeSlots.Clear();
            spawnedBlocks.Clear();

            // 1. Notify Controller
            var controller = Object.FindObjectOfType<NPCController>();
            if (controller != null) controller.Speak("Let me think of a word...");

            // 2. Determine Language
            string language = (context != null && !string.IsNullOrEmpty(context.TargetLanguage)) ? context.TargetLanguage : "English";

            // 3. Generate Word with 30s Timeout
            var generationTask = GenerateWordFromLLM(llmService, language);
            var timeoutTask = Task.Delay(30000);

            var completedTask = await Task.WhenAny(generationTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                targetWord = "TIMEOUT";
                Debug.LogWarning("[SpellingGame] LLM Timeout. Fallback: TIMEOUT");
            }
            else
            {
                targetWord = await generationTask;
                if (string.IsNullOrEmpty(targetWord)) targetWord = "HOUSE";
            }

            if (controller != null)
            {
                controller.Speak($"Let's practice. Spell {targetWord}.");
            }

            Transform cam = Camera.main != null ? Camera.main.transform : null;
            if (cam == null) return LLMActionResult.CreateFailure("No Camera");

            // 4. Calculate Position
            Vector3 flatForward = new Vector3(cam.forward.x, 0, cam.forward.z).normalized;
            Vector3 flatRight = new Vector3(cam.right.x, 0, cam.right.z).normalized;

            Vector3 slotCenter = cam.position + (flatForward * SLOT_DISTANCE);
            slotCenter.y = GAME_HEIGHT;

            Vector3 blockCenter = cam.position + (flatForward * BLOCK_DISTANCE);
            blockCenter.y = GAME_HEIGHT;

            Quaternion uprightRotation = Quaternion.LookRotation(flatForward, Vector3.up);

            // 5. Spawn Elements
            SpawnSlots(slotCenter, flatRight, uprightRotation);
            SpawnScrambledBlocks(blockCenter, flatRight, uprightRotation);

            // 6. Game Loop
            isGameRunning = true;
            _ = RunGameLoop(controller);

            return LLMActionResult.CreateSuccess($"Game started with word {targetWord}");
        }

        private async Task RunGameLoop(NPCController controller)
        {
            while (isGameRunning)
            {
                await Task.Delay(500);
                if (!Application.isPlaying) break;

                if (CheckIfWon())
                {
                    isGameRunning = false;

                    if (controller != null)
                    {
                        controller.PlaySuccessAnimation();
                        controller.Speak($"Correct! {targetWord} is right!");
                    }

                    // NEW: Wait 3 seconds to celebrate, then cleanup
                    await Task.Delay(3000);
                    CleanupGame();

                    break;
                }
            }
        }

        // --- NEW CLEANUP METHOD ---
        private void CleanupGame()
        {
            // Destroy all slots
            foreach (var slot in activeSlots)
            {
                if (slot != null && slot.gameObject != null)
                {
                    Object.Destroy(slot.gameObject);
                }
            }
            activeSlots.Clear();

            // Destroy all blocks (cubes)
            foreach (var block in spawnedBlocks)
            {
                if (block != null)
                {
                    Object.Destroy(block);
                }
            }
            spawnedBlocks.Clear();

            Debug.Log("[SpellingGame] Cleanup complete. Objects destroyed.");
        }

        private void SpawnSlots(Vector3 center, Vector3 right, Quaternion rotation)
        {
            GameObject slotPrefab = Resources.Load<GameObject>("Games/LetterSlot_Prefab");
            if (!slotPrefab) return;

            float totalWidth = (targetWord.Length - 1) * SLOT_SPACING;
            Vector3 startPos = center - (right * (totalWidth * 0.5f));

            for (int i = 0; i < targetWord.Length; i++)
            {
                Vector3 pos = startPos + (right * (i * SLOT_SPACING));
                GameObject slot = Object.Instantiate(slotPrefab, pos, rotation);
                slot.transform.localScale = SLOT_SCALE;

                var rb = slot.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }

                var slotScript = slot.GetComponent<LetterSlot>();
                if (slotScript != null)
                {
                    slotScript.requiredLetter = targetWord[i].ToString().ToUpper();
                    activeSlots.Add(slotScript);
                }
            }
        }

        private void SpawnScrambledBlocks(Vector3 center, Vector3 right, Quaternion rotation)
        {
            GameObject blockPrefab = Resources.Load<GameObject>("Games/LetterBlock_Prefab");
            if (!blockPrefab) return;

            List<char> letters = new List<char>(targetWord.ToCharArray());
            string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            for (int i = 0; i < EXTRA_LETTERS_COUNT; i++)
            {
                letters.Add(alphabet[Random.Range(0, alphabet.Length)]);
            }
            Shuffle(letters);

            float totalWidth = (letters.Count - 1) * BLOCK_SPACING;
            Vector3 startPos = center - (right * (totalWidth * 0.5f));

            for (int i = 0; i < letters.Count; i++)
            {
                Vector3 pos = startPos + (right * (i * BLOCK_SPACING));

                GameObject block = Object.Instantiate(blockPrefab, pos, rotation);
                block.transform.localScale = Vector3.one * BLOCK_SCALE;

                // NEW: Add to list so we can delete it later
                spawnedBlocks.Add(block);

                var rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.useGravity = false;
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
#else
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
#endif
                }

                var blockScript = block.GetComponent<LetterBlock>();
                if (blockScript != null)
                {
                    blockScript.SetLetter(letters[i].ToString().ToUpper());
                }
            }
        }

        private void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private bool CheckIfWon()
        {
            if (activeSlots.Count == 0) return false;
            int correctCount = 0;
            foreach (var slot in activeSlots)
            {
                if (slot.isFilled) correctCount++;
            }
            return correctCount == activeSlots.Count;
        }

        // --- HELPER METHODS FOR LLM GENERATION ---

        private async Task<string> GenerateWordFromLLM(ILLMService llmService, string language)
        {
            string lengthInstruction = (Random.value > 0.5f) ? "5 to 7 letters" : "3 to 10 letters";
            string systemPrompt =
                $"You are a strict database generator. Provide a single random simple noun in {language}. " +
                $"The word must be {lengthInstruction} long. " +
                "Output ONLY the word itself. No punctuation, no explanation.";

            try
            {
                string response = await llmService.GenerateResponseAsync(
                    "Generate word",
                    systemPrompt,
                    new List<ConversationMessage>()
                );
                return CleanResponse(response);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"LLM Error: {ex.Message}");
                return "";
            }
        }

        private string CleanResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return "";
            string[] parts = response.Trim().Split(' ');
            string word = parts[parts.Length - 1];
            word = Regex.Replace(word, @"[^\w]", "");
            return word.ToUpperInvariant();
        }
    }
}