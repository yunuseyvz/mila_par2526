using System.IO;
using UnityEngine;

namespace LanguageTutor.Learning
{
    /// <summary>
    /// Minimal test - just create JSON file immediately
    /// </summary>
    public class VocabularyProgressTest : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[VocabularyProgressTest] Awake called!");
            string filePath = Path.Combine(Application.persistentDataPath, "vocabulary_progress.json");
            Debug.Log($"[VocabularyProgressTest] Will save to: {filePath}");
            
            // Write minimal test JSON
            string json = "{\"data\":[{\"label\":\"test\",\"count\":0,\"lastAsked\":null,\"correctCount\":0,\"incorrectCount\":0}]}";
            
            try
            {
                File.WriteAllText(filePath, json);
                Debug.Log($"[VocabularyProgressTest] ✅ File written successfully!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VocabularyProgressTest] ❌ Failed to write: {ex.Message}");
            }
        }
    }
}
