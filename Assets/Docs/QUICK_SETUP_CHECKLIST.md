# Quick Setup Checklist

## Avatar Animation System - Quick Setup

### ✅ Step-by-Step Setup

#### 1. Configure Animator Controller
- [ ] Open `Assets/Animations/CharacterController.controller`
- [ ] Ensure you have 3 states: **Idle**, **Thinking**, **Talking**
- [ ] Add Parameters (Type: Trigger):
  - [ ] `Idle`
  - [ ] `Thinking`
  - [ ] `Talking`
- [ ] Create Transitions:
  - [ ] Any State → Idle (Condition: Idle trigger)
  - [ ] Any State → Thinking (Condition: Thinking trigger)
  - [ ] Any State → Talking (Condition: Talking trigger)
- [ ] Set Transition Settings:
  - [ ] Uncheck "Has Exit Time"
  - [ ] Set "Transition Duration" to 0.1-0.2

#### 2. Add Component to Avatar
- [ ] Select your Character/Avatar GameObject in the scene
- [ ] Click "Add Component"
- [ ] Search for "AvatarAnimationController"
- [ ] Drag the Animator component to the "Animator" field
- [ ] Verify trigger names match your animator (or customize them)

#### 3. Link to NPCController
- [ ] Find the NPCController GameObject in your scene
- [ ] Locate the "Components" section in the Inspector
- [ ] Drag your Avatar (with AvatarAnimationController) to the "Avatar Animation Controller" field

#### 4. Test It!
- [ ] Right-click on AvatarAnimationController component
- [ ] Test each animation using context menu
- [ ] Play the scene and talk to the NPC
- [ ] Observe animation changes during conversation

---

## Animator Controller Setup (Visual Guide)

### Parameters Panel
```
Name         Type
─────────────────
Idle         Trigger
Thinking     Trigger
Talking      Trigger
```

### States and Transitions
```
┌─────────────┐
│  Any State  │
└─────────────┘
      │
      ├──(Idle)───────→ ┌─────────┐
      │                  │  Idle   │ ← Default State
      │                  └─────────┘
      │
      ├──(Thinking)───→ ┌───────────┐
      │                  │ Thinking  │
      │                  └───────────┘
      │
      └──(Talking)────→ ┌───────────┐
                         │  Talking  │
                         └───────────┘
```

### Transition Inspector Settings
```
Transition: Any State → Idle
───────────────────────────────
Conditions:
  • Idle (Trigger)

Settings:
  ☐ Has Exit Time
  ☐ Fixed Duration
  Transition Duration: 0.15
```

---

## Testing in Play Mode

### Expected Behavior:

1. **Start Scene**
   - Animation: **Idle**
   - Status: "Talk" button enabled

2. **Press Talk Button**
   - Animation: **Idle** (still idle while listening)
   - Status: "Listening..."

3. **Stop Recording**
   - Animation: **Thinking** (processing begins)
   - Status: "Transcribing..." → "Thinking..." → "Generating voice..."

4. **Audio Plays**
   - Animation: **Talking**
   - Status: "NPC is speaking..."

5. **Audio Finishes**
   - Animation: **Idle**
   - Status: Ready for next interaction

---

## Common Issues & Fixes

### Issue: No Animation Changes
**Fix:** Check that AvatarAnimationController is assigned in NPCController's Inspector

### Issue: Wrong Animation Plays
**Fix:** Verify trigger names match exactly (case-sensitive):
- Animator Parameters: `Idle`, `Thinking`, `Talking`
- AvatarAnimationController Inspector: Same names

### Issue: Animation Transitions Are Jerky
**Fix:** Increase "Transition Duration" to 0.2-0.3 seconds

### Issue: Avatar Stays in Thinking Forever
**Fix:** Ensure Update() method is running - check console for any errors

---

## Inspector Configuration Example

### AvatarAnimationController Component
```
┌─────────────────────────────────────────┐
│ Avatar Animation Controller (Script)   │
├─────────────────────────────────────────┤
│ ► Animation Components                  │
│   Animator: [Character Animator]       │
│                                         │
│ ► Animation Trigger Names               │
│   Idle Trigger: Idle                    │
│   Thinking Trigger: Thinking            │
│   Talking Trigger: Talking              │
│                                         │
│ ► Animation State Names (Optional)      │
│   Idle State Name: Idle                 │
│   Thinking State Name: Thinking         │
│   Talking State Name: Talking           │
└─────────────────────────────────────────┘
```

### NPCController Component
```
┌─────────────────────────────────────────┐
│ NPC Controller (Script)                 │
├─────────────────────────────────────────┤
│ ► Configuration                         │
│   LLM Config: [...]                     │
│   TTS Config: [...]                     │
│   STT Config: [...]                     │
│   Conversation Config: [...]            │
│                                         │
│ ► Components                            │
│   Whisper Manager: [WhisperManager]    │
│   NPC View: [NPCView]                   │
│   Avatar Animation Controller:          │
│     [YourAvatar/Character]  ← ADD THIS! │
│                                         │
│ ► Action Mode                           │
│   Default Action Mode: Chat             │
└─────────────────────────────────────────┘
```

---

## Debug Console Messages

When working correctly, you'll see these logs:

```
[NPCController] Initialized successfully
[AvatarAnimationController] Animation set to: Idle

[User presses Talk button]
[NPCController] Recording completed, starting processing...

[AvatarAnimationController] Animation set to: Thinking
[ConversationPipeline] Stage 1: Transcribing speech...
[ConversationPipeline] Stage 2: Generating LLM response...
[ConversationPipeline] Stage 3: Synthesizing speech...

[AvatarAnimationController] Animation set to: Talking
[NPCView] Playing audio clip: ...

[After audio finishes]
[AvatarAnimationController] Animation set to: Idle
```

---

## Files Modified

- ✅ Created: `Assets/Scripts/Core/AvatarAnimationController.cs`
- ✅ Modified: `Assets/Scripts/Core/NPCController.cs`
- ✅ Created: `Assets/Docs/AVATAR_ANIMATION_SETUP.md`
- ✅ Created: `Assets/Docs/QUICK_SETUP_CHECKLIST.md` (this file)

---

## Next Steps

1. Follow the checklist above
2. Test in Play mode
3. Adjust animation transitions for your preference
4. Consider adding lip sync using OVRLipSync (already in your project)

**Need Help?** Check the detailed guide at `Assets/Docs/AVATAR_ANIMATION_SETUP.md`
