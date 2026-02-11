using UnityEngine;
using LanguageTutor.Core;

/// <summary>
/// Example: How to integrate TutorContextComponent with NPCController
/// for room-aware tutoring experiences.
/// 
/// ADD THIS SCRIPT TO YOUR NPC GAMEOBJECT to enable AR-aware teaching.
/// Or copy/adapt the patterns into your existing NPCController.
/// </summary>
public class TutorRoomAwarenessIntegration : MonoBehaviour
{
    [SerializeField] private NPCController npcController;
    [SerializeField] private TutorContextComponent roomContext;

    private void Start()
    {
        if (npcController == null)
            npcController = GetComponent<NPCController>();
        
        if (roomContext == null)
            roomContext = FindObjectOfType<TutorContextComponent>();

        if (roomContext == null)
            Debug.LogWarning("[TutorIntegration] TutorContextComponent not found. Room context disabled.");
    }

    /// <summary>
    /// EXAMPLE 1: Greet the student with observed objects
    /// </summary>
    public void GreetWithRoomContext()
    {
        if (roomContext == null || npcController == null)
            return;

        string greeting;
        if (roomContext.HasDetectedObjects())
        {
            greeting = $"Hi! I can see you're in a room with {roomContext.GetDetectedObjectLabels().Count} different types of objects. " +
                       roomContext.GenerateRoomTourPrompt();
        }
        else
        {
            greeting = "Hi there! I'm looking around your space. Once I see some objects, we can start learning together!";
        }

        npcController.Speak(greeting);
    }

    /// <summary>
    /// EXAMPLE 2: Teach vocabulary about the closest object
    /// </summary>
    public void TeachClosestObject()
    {
        if (roomContext == null || npcController == null)
            return;

        var obj = roomContext.GetClosestObject();
        if (obj == null)
        {
            npcController.Speak("I don't see any objects nearby yet.");
            return;
        }

        string teaching = $"Look at that {obj.Label}! " +
                         $"In English, we call this a '{obj.Label}'. " +
                         $"Can you repeat after me: {obj.Label}?";

        npcController.Speak(teaching);
    }

    /// <summary>
    /// EXAMPLE 3: Ask student to identify a specific object type
    /// </summary>
    public void AskAboutObjectType(string label)
    {
        if (roomContext == null || npcController == null)
            return;

        int count = roomContext.CountObjectsByLabel(label);
        if (count == 0)
        {
            npcController.Speak($"I don't see any {label}s in your room right now.");
            return;
        }

        string plural = count > 1 ? "s" : "";
        string question = $"I can see {count} {label}{plural} in your room. " +
                         $"Can you point to one and tell me what color it is in English?";

        npcController.Speak(question);
    }

    /// <summary>
    /// EXAMPLE 4: Generate a contextualized LLM system prompt
    /// This enriches the tutor's responses with knowledge of the room.
    /// 
    /// USE THIS: Before sending a message to your LLM service, prepend this context.
    /// </summary>
    public string GetEnrichedSystemPrompt(string baseSystemPrompt)
    {
        if (roomContext == null)
            return baseSystemPrompt;

        // Example: append room context to system prompt for LLM
        return baseSystemPrompt + "\n\n" + roomContext.BuildTutorSystemPromptContext();
    }

    /// <summary>
    /// EXAMPLE 5: Create an interactive room tour
    /// Speak about each unique object type in the room.
    /// </summary>
    public void StartInteractiveRoomTour()
    {
        if (roomContext == null || npcController == null)
            return;

        var labels = roomContext.GetDetectedObjectLabels();
        if (labels.Count == 0)
        {
            npcController.Speak("Let me wait for the room to load...");
            return;
        }

        string tourText = "Let me show you what I see:\n";
        foreach (var label in labels)
        {
            int count = roomContext.CountObjectsByLabel(label);
            tourText += $"• {count} {label}{(count > 1 ? "s" : "")}\n";
        }

        npcController.Speak(tourText);
    }

    /// <summary>
    /// EXAMPLE 6: Recent detections (newest first)
    /// Useful for teaching newly discovered objects
    /// </summary>
    public void TeachRecentDetections()
    {
        if (roomContext == null || npcController == null)
            return;

        var recent = roomContext.GetRecentDetections(3);
        if (recent.Count == 0)
        {
            npcController.Speak("I'm still scanning the room...");
            return;
        }

        string text = "I just spotted these:\n";
        foreach (var obj in recent)
        {
            text += $"• {obj.Label}\n";
        }

        npcController.Speak(text);
    }

    // =========================================================================
    // INTEGRATION PATTERN: Use in your ConversationPipeline
    // =========================================================================

    // If you're modifying the LLM's system prompt, use it like this:
    //
    // Example in ConversationPipeline or LLMActionExecutor:
    //
    //     public async Task<string> ExecuteAction(...)
    //     {
    //         string systemPrompt = _config.SystemPrompt;
    //
    //         // ADD THIS:
    //         var roomContext = FindObjectOfType<TutorContextComponent>();
    //         if (roomContext != null)
    //         {
    //             systemPrompt = roomContext.BuildTutorSystemPromptContext();
    //         }
    //
    //         var response = await _llmService.SendMessage(systemPrompt, userMessage);
    //         return response;
    //     }
}
