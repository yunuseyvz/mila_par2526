using UnityEngine;

namespace LanguageTutor.Data
{
    /// <summary>
    /// Defines a roleplay scenario for conversation practice.
    /// </summary>
    [CreateAssetMenu(fileName = "RoleplayScenario", menuName = "Language Tutor/Roleplay Scenario", order = 5)]
    public class RoleplayScenarioConfig : ScriptableObject
    {
        [Header("Scenario Info")]
        [Tooltip("Name of the scenario (e.g., 'Coffee Shop Waiter')")]
        public string scenarioName = "Coffee Shop Waiter";

        [Tooltip("Short description of the scenario")]
        [TextArea(2, 3)]
        public string description = "Practice ordering at a coffee shop";

        [Header("AI Role Configuration")]
        [Tooltip("The role the AI will play (e.g., 'waiter', 'receptionist', 'tour guide')")]
        public string aiRole = "waiter";

        [Tooltip("Setting/location for the conversation (e.g., 'busy coffee shop', 'hotel lobby')")]
        public string setting = "a busy coffee shop";

        [Tooltip("System prompt that defines the AI's behavior and personality")]
        [TextArea(5, 10)]
        public string systemPrompt = "You are a friendly waiter working at a busy coffee shop. You greet customers warmly, take their orders, suggest menu items, and answer questions about the coffee and pastries. Stay in character and be helpful and professional. Keep responses to 1-2 sentences.";

        [Header("Optional Context")]
        [Tooltip("Additional context or constraints (e.g., 'The customer is ordering breakfast')")]
        [TextArea(2, 4)]
        public string additionalContext = "";

        [Tooltip("Suggested phrases the learner can practice (optional)")]
        public string[] suggestedPhrases = new string[]
        {
            "Can I have a coffee, please?",
            "What do you recommend?",
            "How much is that?",
            "Can I pay by card?"
        };
    }
}
