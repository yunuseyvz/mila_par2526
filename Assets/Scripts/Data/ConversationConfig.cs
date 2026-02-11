using UnityEngine;

namespace LanguageTutor.Data
{
    /// <summary>
    /// Configuration for conversation language and game mode.
    /// Create via: Assets -> Create -> Language Tutor -> Conversation Config
    /// </summary>
    [CreateAssetMenu(fileName = "ConversationConfig", menuName = "Language Tutor/Conversation Config", order = 4)]
    public class ConversationConfig : ScriptableObject
    {
        [Header("Language")]
        [Tooltip("Language used for the conversation")]
        public ConversationLanguage language = ConversationLanguage.English;

        [Header("Game Mode")]
        [Tooltip("Active game mode for the conversation")]
        public ConversationGameMode gameMode = ConversationGameMode.FreeTalk;
    }

    public enum ConversationLanguage
    {
        English,
        German
    }

    public enum ConversationGameMode
    {
        FreeTalk,
        WordClouds,
        ObjectTagging,
        RolePlay
    }
}
