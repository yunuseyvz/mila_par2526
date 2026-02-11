# Tutor Room Awareness Integration Guide

## Overview
The tutor can now access and reference detected objects in the room during conversations. This enables AR-aware teaching scenarios like:
- "I see a chair nearby. Can you tell me what color it is?"
- "You have 3 tables in this room. Let's learn their names in English."
- Context-enriched LLM prompts that make responses location-aware

---

## Quick Setup

### 1. Add TutorContextComponent to Your Tutor NPC
```
1. In Hierarchy, select your NPC GameObject
2. Add Component → TutorContextComponent
3. The component auto-finds DetectedObjectRegistry and ObjectDetectionListRecorder
   (or manually assign them in the Inspector if needed)
```

### 2. Access Detected Objects in Code

#### From NPCController or any tutor-facing class:
```csharp
private TutorContextComponent _roomContext;

private void Awake()
{
    _roomContext = FindObjectOfType<TutorContextComponent>();
}

public void OnTeachButtonClicked()
{
    // Get all detected objects
    var allObjects = _roomContext.GetAllDetectedObjects();
    
    // Find specific types
    var chairs = _roomContext.FindObjectsByLabel("chair");
    
    // Get the closest object
    var nearest = _roomContext.GetClosestObject();
    
    // Build natural language context
    string context = _roomContext.BuildRoomContextDescription();
}
```

---

## API Reference

### Query Methods

#### `GetAllDetectedObjects()`
Returns all currently detected objects in the room.
```csharp
var objects = _roomContext.GetAllDetectedObjects();
// objects is IReadOnlyList<DetectedObjectRegistry.Entry>
```

#### `FindObjectsByLabel(string label)`
Find all objects with a specific name (e.g., "chair", "table").
```csharp
var chairs = _roomContext.FindObjectsByLabel("chair");  // Returns List<Entry>
```

#### `CountObjectsByLabel(string label)`
Quick count of how many objects of a type are detected.
```csharp
int chairCount = _roomContext.CountObjectsByLabel("chair");
```

#### `GetClosestObject()`
Find the nearest detected object to the player/camera.
```csharp
var nearest = _roomContext.GetClosestObject();  // Returns Entry or null
```

#### `GetClosestObjectOfType(string label)`
Find the nearest object of a specific type.
```csharp
var closestChair = _roomContext.GetClosestObjectOfType("chair");
```

#### `GetDetectedObjectLabels()`
Get all unique object type names detected.
```csharp
var labels = _roomContext.GetDetectedObjectLabels();  // List<string>
// e.g., ["chair", "table", "door", "window"]
```

---

### Context Generation Methods

#### `BuildRoomContextDescription()`
Generate a natural language description of the room.
```csharp
string description = _roomContext.BuildRoomContextDescription();
// Output:
// "Detected 7 objects in the room:
//  • 3 chairs (avg confidence: 92%)
//  • 1 table (confidence: 88%)
//  • 2 doors (avg confidence: 85%)"
```

#### `BuildTutorSystemPromptContext()`
Generate system prompt context for the LLM tutor. **This is the key for room-aware AI responses.**
```csharp
string systemContext = _roomContext.BuildTutorSystemPromptContext();
// Inject this into your LLM's system prompt before sending messages
```

#### `DescribeObject(Entry entry)`
Get a human-readable description of a specific object.
```csharp
var chair = _roomContext.FindObjectsByLabel("chair")[0];
string desc = _roomContext.DescribeObject(chair);
// "chair (confidence: 92%) at position (1.2, 0.8, -3.4)"
```

---

### Teaching Helper Methods

#### `GenerateRandomObjectTeachingPrompt()`
Auto-generate a teaching prompt about a random detected object.
```csharp
string prompt = _roomContext.GenerateRandomObjectTeachingPrompt();
npcController.Speak(prompt);
// "Now let's talk about that chair nearby. Can you describe it?..."
```

#### `GenerateRoomTourPrompt()`
Generate an icebreaker prompt about what's in the room.
```csharp
string prompt = _roomContext.GenerateRoomTourPrompt();
npcController.Speak(prompt);
// "I can see chairs, table, and 2 more objects in your room..."
```

#### `GetRecentDetections(int count)`
Get the most recently detected objects (for teaching newest items first).
```csharp
var recent = _roomContext.GetRecentDetections(5);  // Last 5 detected
```

---

### State Helpers

#### `GetTotalDetectedObjectCount()`
How many objects total are detected in the room.
```csharp
int total = _roomContext.GetTotalDetectedObjectCount();
```

#### `HasDetectedObjects()`
Quick check if any objects are detected.
```csharp
if (_roomContext.HasDetectedObjects())
{
    // Start room tour
}
```

#### `HasObjectsOfType(string label)`
Check if objects of a specific type exist.
```csharp
if (_roomContext.HasObjectsOfType("chair"))
{
    // Ask about chairs
}
```

---

## Integration Patterns

### Pattern 1: Enrich LLM System Prompt
Make the tutor aware of the room when generating responses.

**In your LLMActionExecutor or ConversationPipeline:**
```csharp
public async Task<string> ExecuteAction(string userMessage)
{
    string systemPrompt = _config.SystemPrompt;

    // ADD THIS - enriches the prompt with room context
    var roomContext = FindObjectOfType<TutorContextComponent>();
    if (roomContext != null)
    {
        systemPrompt += "\n\n" + roomContext.BuildTutorSystemPromptContext();
    }

    var response = await _llmService.SendMessage(systemPrompt, userMessage);
    return response;
}
```

Now your LLM will "know" about detected objects and can reference them naturally.

---

### Pattern 2: Context-Aware Greeting
Greet the user differently based on what's in the room.

```csharp
public async void Greet()
{
    string greeting;
    
    if (_roomContext.HasDetectedObjects())
    {
        var labels = _roomContext.GetDetectedObjectLabels();
        greeting = $"Hi! I can see you're in a room with {labels.Count} types of objects. " +
                   _roomContext.GenerateRoomTourPrompt();
    }
    else
    {
        greeting = "Hi! I'm setting up. Let me scan the room first...";
    }

    await npcController.Speak(greeting);
}
```

---

### Pattern 3: Interactive Object Teaching
Let the user point at an object and have the tutor teach about it.

```csharp
public void OnObjectPointed(string detectedLabel)
{
    var obj = _roomContext.GetClosestObjectOfType(detectedLabel);
    if (obj == null)
    {
        npcController.Speak($"I don't see a {detectedLabel} there.");
        return;
    }

    string teaching = $"Good observation! That's a '{obj.Label}'. " +
                     $"In English, it's pronounced: {obj.Label}. " +
                     $"Can you repeat it?";

    npcController.Speak(teaching);
}
```

---

### Pattern 4: Vocabulary Exercise from Room
Generate vocabulary exercises based on what's actually in the room.

```csharp
public void StartVocabularyExercise()
{
    var recentDetections = _roomContext.GetRecentDetections(3);
    
    string exercise = "Let's learn these words. Repeat after me:\n";
    foreach (var obj in recentDetections)
    {
        exercise += $"• {obj.Label}\n";
    }

    npcController.Speak(exercise);
}
```

---

## Data Structure

Objects in the registry have this structure:
```csharp
public class Entry
{
    public string Label;              // e.g., "chair", "table", "door"
    public float Confidence;          // 0.0 to 1.0 (how sure the detector is)
    public Vector3 Position;          // World position in the room
    public float LastSeenTime;        // Time.time when last updated
}
```

Access them:
```csharp
var entry = _roomContext.GetClosestObject();
Debug.Log($"Detected: {entry.Label}");
Debug.Log($"Position: {entry.Position}");
Debug.Log($"Confidence: {entry.Confidence * 100}%");
Debug.Log($"Last seen: {entry.LastSeenTime}");
```

---

## Example: Full Teaching Sequence

```csharp
public async void RunFullRoomLearningSequence()
{
    // 1. Greet with context
    npcController.Speak(_roomContext.GenerateRoomTourPrompt());
    await Task.Delay(2000);

    // 2. Pick an object and teach about it
    var nearest = _roomContext.GetClosestObject();
    if (nearest != null)
    {
        npcController.Speak($"Let's learn about this {nearest.Label}");
        await Task.Delay(1000);
        npcController.Speak($"In English: {nearest.Label}. Can you say it?");
    }

    // 3. Ask the student to identify items
    var labels = _roomContext.GetDetectedObjectLabels();
    if (labels.Count > 0)
    {
        string question = $"I see a {labels[0]}. Can you find it and point to it?";
        npcController.Speak(question);
    }
}
```

---

## Debugging

### Check if Component is Connected
```csharp
var context = FindObjectOfType<TutorContextComponent>();
if (context == null)
{
    Debug.LogError("TutorContextComponent not in scene!");
}

if (!context.HasDetectedObjects())
{
    Debug.LogWarning("No objects detected. Check ObjectDetectionListRecorder is active.");
}
```

### View Detected Objects in Inspector
Once TutorContextComponent is in play mode:
- It automatically finds the registry and recorder
- Enable "logToConsole" in the component for debug logs
- Watch the Console tab as objects are detected

### Example Debug Output
```
[TutorContextComponent] Initialized. Registry: FOUND, Recorder: FOUND
[TutorContextComponent] Detected 2 new objects this frame. Total in registry: 8
Detected 5 objects in the room:
  • 3 chairs (avg confidence: 91%)
  • 1 table (confidence: 87%)
  • 1 door (confidence: 89%)
```

---

## Performance Considerations

- **GetAllDetectedObjects()**: O(1) - returns a reference to the list
- **FindObjectsByLabel()**: O(n) where n = total objects - creates a new list
- **GetClosestObject()**: O(n) - iterates through all objects
- For 100+ objects, cache results if calling frequently in same frame

Example:
```csharp
private DetectedObjectRegistry.Entry _cachedClosest;

public DetectedObjectRegistry.Entry GetCachedClosest()
{
    if (Time.frameCount != _lastFrameChecked)
    {
        _cachedClosest = GetClosestObject();
        _lastFrameChecked = Time.frameCount;
    }
    return _cachedClosest;
}
```

---

## Next Steps

1. ✅ Add TutorContextComponent to your NPC GameObject
2. ✅ Test by calling `GetAllDetectedObjects()` and log the results
3. ✅ Enrich your LLM system prompt using `BuildTutorSystemPromptContext()`
4. ✅ Create room-aware conversation flows
5. ✅ Build interactive teaching scenarios that reference real objects

---

## Questions?

- **"Objects aren't being detected"** → Ensure ObjectDetectionListRecorder has both agent/visualizer and `MRUK_INSTALLED` define is set
- **"Tutor doesn't see context"** → Call `BuildTutorSystemPromptContext()` before sending to LLM
- **"Positions seem wrong"** → Positions are in world space; use `Camera.main.transform.position` as reference for distance calculations
