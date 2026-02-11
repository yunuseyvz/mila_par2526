using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using LanguageTutor.Services.LLM;
using LanguageTutor.Core;
using LanguageTutor.Games.Spelling;

namespace LanguageTutor.Actions
{
public class SpellingGameAction : ILLMAction
{
// --- CONFIGURATION ---

// Spacing between objects (tighter since objects are smaller now)
private const float SLOT_SPACING = 0.25f;
private const float BLOCK_SPACING = 0.20f;

    // Height: Slots at distinct height, Blocks higher up
    private const float SLOT_HEIGHT = 1.5f; 
    private const float BLOCK_HEIGHT = 1.9f; 

    // Depth: Slots in back (0.6m), Blocks in front (0.8m)
    private const float SLOT_DISTANCE = 0.6f; 
    private const float BLOCK_DISTANCE = 0.8f; // Further away (User: "deutlich weiter weg")

    // Horizontal offset: Shift everything to the RIGHT (reduced from 0.6f)
    private const float HORIZONTAL_SHIFT = 0.3f; // More left, but not center (User request: "nicht in die mitte")
    private const float BLOCK_OFFSET_RANGE = 0.3f;

// Number of extra "wrong" letters
private const int EXTRA_LETTERS_COUNT = 3;

// SCALE: Slightly smaller to reduce overlap with UI
private const float BLOCK_SCALE = 0.12f;
private Vector3 SLOT_SCALE = new Vector3(0.14f, 0.02f, 0.14f);

// --- STATE ---
    private string targetWord;
    // Placeholder vocabulary list simulating object detection results
    private List<string> activeVocabulary = new List<string> 
    { 
        "laptop", "lamp", "person", "mouse", "bottle", "screen" 
    };
    private List<LetterSlot> activeSlots = new List<LetterSlot>();
    private List<GameObject> spawnedObjects = new List<GameObject>(); // Track everything for cleanup
    private bool isGameRunning = false;

public string GetActionName() => "SpellingGame";
public bool CanExecute(LLMActionContext context) => true;

    public async Task<LLMActionResult> ExecuteAsync(ILLMService llmService, LLMActionContext context)
    {
        // Pick a random word from the placeholder list
        if (activeVocabulary != null && activeVocabulary.Count > 0)
        {
            targetWord = activeVocabulary[Random.Range(0, activeVocabulary.Count)].ToUpper();
        }
        else
        {
            targetWord = "CAT"; // Fallback
        }

        // Using standard FindObjectOfType to ensure compatibility
        var controller = Object.FindObjectOfType<NPCController>();

    if (controller != null)
    {
        controller.Speak($"Let's practice. Spell {targetWord}.");
    }

    Transform cam = Camera.main != null ? Camera.main.transform : null;
    if (cam == null) return LLMActionResult.CreateFailure("No Camera");

    // 1. Calculate Position (Stable & Flat)
    Vector3 flatForward = new Vector3(cam.forward.x, 0, cam.forward.z).normalized;
    Vector3 flatRight = new Vector3(cam.right.x, 0, cam.right.z).normalized;

        // Slots: Shifted RIGHT, slightly further
        Vector3 slotCenter = cam.position + (flatForward * SLOT_DISTANCE) + (flatRight * HORIZONTAL_SHIFT);
        slotCenter.y = SLOT_HEIGHT;

        // Blocks: Shifted RIGHT, closer, higher up
        Vector3 blockCenter = cam.position + (flatForward * BLOCK_DISTANCE) + (flatRight * HORIZONTAL_SHIFT);
        blockCenter.y = BLOCK_HEIGHT;

    // Rotation: Strictly forward (no tilting)
    Quaternion uprightRotation = Quaternion.LookRotation(flatForward, Vector3.up);

    // 2. Spawn Elements
    SpawnSlots(slotCenter, flatRight, uprightRotation);
    SpawnScrambledBlocks(blockCenter, flatRight, uprightRotation);

    // 3. Game Loop
    isGameRunning = true;
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

            // --- CLEANUP AFTER 10 SECONDS ---
            await Task.Delay(10000); 
            CleanupGame();
        }
    }

    return LLMActionResult.CreateSuccess("Game Won");
}

private void CleanupGame()
{
    foreach (var obj in spawnedObjects)
    {
        if (obj != null) Object.Destroy(obj);
    }
    spawnedObjects.Clear();
    activeSlots.Clear();
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
        spawnedObjects.Add(slot);

        // FIX: Disable physics to prevent wobbling
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

    // Prepare letters & shuffle
    List<char> letters = new List<char>(targetWord.ToCharArray());
    string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    for (int i = 0; i < EXTRA_LETTERS_COUNT; i++)
    {
        letters.Add(alphabet[Random.Range(0, alphabet.Length)]);
    }
    Shuffle(letters);

    // Positioning: Exact Line
    float totalWidth = (letters.Count - 1) * BLOCK_SPACING;
    Vector3 startPos = center - (right * (totalWidth * 0.5f));

    for (int i = 0; i < letters.Count; i++)
    {
        Vector3 pos = startPos + (right * (i * BLOCK_SPACING));

        GameObject block = Object.Instantiate(blockPrefab, pos, rotation);
        block.transform.localScale = Vector3.one * BLOCK_SCALE;
        spawnedObjects.Add(block);

        // --- GRAVITY FIX (Standard API) ---
        var rb = block.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Disable gravity -> They float securely!
            rb.useGravity = false;

            // Stop movement (Standard Unity command)
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        // ----------------------------------

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
}
}