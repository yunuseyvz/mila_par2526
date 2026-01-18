# Avatar Animation Setup Guide

This guide explains how to set up avatar animations that respond to the conversation pipeline stages (thinking during STT/LLM/TTS processing, talking when TTS plays).

## Overview

The system now includes:
- **Thinking Animation**: Plays during STT transcription, LLM processing, and TTS generation
- **Talking Animation**: Plays when TTS audio begins playing
- **Idle Animation**: Default state when not processing or speaking

## Setup Steps

### 1. Prepare Your Animator Controller

Your animator controller should have three animation states:
- **Idle** - Default resting state
- **Thinking** - Avatar appears to be pondering/processing
- **Talking** - Avatar appears to be speaking

#### Create Transitions:
1. Open your `CharacterController` animator controller in `Assets/Animations/`
2. Create transitions between states:
   - Any State → Idle (with trigger: `Idle`)
   - Any State → Thinking (with trigger: `Thinking`)
   - Any State → Talking (with trigger: `Talking`)

#### Recommended Transition Settings:
- **Has Exit Time**: Disabled (for responsive transitions)
- **Fixed Duration**: Disabled
- **Transition Duration**: 0.1-0.25 seconds (for smooth blending)

### 2. Add AvatarAnimationController to Your Scene

1. Select your Avatar/Character GameObject in the scene
2. Add the `AvatarAnimationController` component:
   - Go to **Add Component** → **Language Tutor** → **Core** → **Avatar Animation Controller**
3. Assign the **Animator** reference (drag your character's Animator component)
4. Configure trigger names (if different from defaults):
   - Idle Trigger: `Idle`
   - Thinking Trigger: `Thinking`
   - Talking Trigger: `Talking`

### 3. Link to NPCController

1. Select your NPCController GameObject in the scene
2. In the NPCController component, find the **Components** section
3. Drag your Avatar with the `AvatarAnimationController` component to the **Avatar Animation Controller** field

### 4. Test the Setup

You can test animations directly from the Inspector:
1. Select the GameObject with `AvatarAnimationController`
2. Right-click on the component
3. Choose:
   - **Test Idle Animation**
   - **Test Thinking Animation**
   - **Test Talking Animation**

## How It Works

### Animation Flow

```
User Presses Talk Button
        ↓
   [Listening State]
        ↓
User Stops Recording
        ↓
┌───────────────────────┐
│ STT Processing        │ → Thinking Animation
└───────────────────────┘
        ↓
┌───────────────────────┐
│ LLM Processing        │ → Thinking Animation
└───────────────────────┘
        ↓
┌───────────────────────┐
│ TTS Generation        │ → Thinking Animation
└───────────────────────┘
        ↓
┌───────────────────────┐
│ TTS Audio Plays       │ → Talking Animation
└───────────────────────┘
        ↓
   Audio Finishes
        ↓
   [Idle Animation]
```

### Pipeline Stage Mapping

| Pipeline Stage          | Animation State | UI Message           |
|------------------------|-----------------|---------------------|
| Transcribing           | Thinking        | "Transcribing..."   |
| GeneratingResponse     | Thinking        | "Thinking..."       |
| SynthesizingSpeech     | Thinking        | "Generating voice..."|
| Complete (TTS Playing) | Talking         | "NPC is speaking..."  |
| Complete (Finished)    | Idle            | Ready               |
| Error                  | Idle            | Error message       |

## Code Integration

The animation system integrates with the existing conversation pipeline through events:

### In NPCController.cs:

```csharp
// During pipeline stage changes
private void HandlePipelineStageChanged(PipelineStage stage)
{
    switch (stage)
    {
        case PipelineStage.Transcribing:
        case PipelineStage.GeneratingResponse:
        case PipelineStage.SynthesizingSpeech:
            avatarAnimationController.SetThinking();
            break;
    }
}

// When TTS audio starts playing
if (result.TTSAudioClip != null)
{
    npcView.PlayAudio(result.TTSAudioClip);
    avatarAnimationController.SetTalking();
}

// When audio finishes (in Update)
if (!npcView.IsAudioPlaying())
{
    avatarAnimationController.SetIdle();
}
```

## Customization

### Different Animation Triggers

If your animator uses different trigger names, update them in the Inspector:
1. Select the GameObject with `AvatarAnimationController`
2. Modify the **Animation Trigger Names** section

### Additional Animation States

To add more states (e.g., "Surprised", "Happy"):

1. Update `AnimationState` enum in `AvatarAnimationController.cs`:
```csharp
public enum AnimationState
{
    Idle,
    Thinking,
    Talking,
    Surprised,
    Happy
}
```

2. Add methods to trigger them:
```csharp
public void SetSurprised()
{
    animator.SetTrigger("Surprised");
    _currentState = AnimationState.Surprised;
}
```

3. Integrate with NPCController where needed

### Lip Sync Integration

For more advanced talking animations with lip sync:

1. The system is already set up with Oculus LipSync (see your project structure)
2. You can integrate OVRLipSync with the talking animation:
   - Add `OVRLipSyncContext` to your avatar
   - Add `OVRLipSyncContextMorphTarget` for facial animation
   - The audio from NPCView's AudioSource will drive the lip sync automatically

## Troubleshooting

### Animation Not Playing

**Check:**
- [ ] Animator component is assigned in AvatarAnimationController
- [ ] AvatarAnimationController is linked to NPCController
- [ ] Trigger names match your Animator Controller
- [ ] Transitions exist in the Animator Controller
- [ ] "Has Exit Time" is disabled on transitions for immediate response

### Animation Stuck in One State

**Solution:**
- Ensure transitions can go from "Any State" to each animation state
- Check that the Update() method in NPCController is being called
- Use the context menu tests to verify animations work independently

### Animations Change Too Fast

**Solution:**
- Increase "Transition Duration" in your Animator transitions
- Add a small delay before changing states (if needed)

### Animation Doesn't Match Audio

**Solution:**
- Talking animation is triggered when audio STARTS, not during processing
- Verify `autoPlayTTS` is enabled in ConversationConfig
- Check that audio is actually playing (NPCView logs will show this)

## Performance Notes

- Animation state changes are only triggered when state actually changes (prevents redundant calls)
- Uses triggers instead of booleans for cleaner state machine
- Minimal overhead - just simple method calls during pipeline events

## Example Configuration

Recommended animation settings for natural conversation:

```
Idle Animation:
- Loop: Yes
- Duration: 2-5 seconds
- Should look natural/relaxed

Thinking Animation:
- Loop: Yes
- Duration: 1-3 seconds
- Subtle movements (head tilt, hand to chin, looking around)

Talking Animation:
- Loop: Yes
- Duration: Match typical speech patterns
- Mouth movements, head gestures
- Can be synchronized with audio waveform for advanced setup
```

## See Also

- [ConversationPipeline.cs](../Scripts/Core/ConversationPipeline.cs) - Pipeline stages
- [NPCController.cs](../Scripts/Core/NPCController.cs) - Main integration point
- [AvatarAnimationController.cs](../Scripts/Core/AvatarAnimationController.cs) - Animation control
