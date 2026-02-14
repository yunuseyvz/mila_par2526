<div align="center">

<img src="Assets/Resources/app_icon.png" alt="Mila AR App Icon" width="150" />

# Mila AR: Your Personal Language Tutor
### Praktikum Augmented Reality | LMU Munich | WS25/26

[![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?style=flat&logo=unity)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-Meta_Quest-blue?style=flat&logo=meta)](https://www.meta.com/quest/)
[![Language](https://img.shields.io/badge/Language-C%23-green?style=flat&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)

<br/>

**Mila AR** is a multilingual AR language tutor for Meta Quest. It combines **Speech-to-Text (STT)**, **LLM-based dialogue**, **Text-to-Speech (TTS)**, and **Vision/Object Detection** for room-aware language learning.

</div>

---

## 👥 The Team

| Member | Role/Handle |
| :--- | :--- |
| **Yunus Emre Yavuz** | [@yunuseyvz](https://github.com/yunuseyvz) |
| **Entoni Jombi** | [@saitamaisreal](https://github.com/saitamaisreal) |
| **Anna-Maria Lödige** | [@annamaria-loe](https://github.com/annamaria-loe) |
| **Kevin Kafexhi** | [@Kafexhi](https://github.com/Kafexhi) |

---

## 1) What this app can do

- Multilingual language teaching and conversation practice
- Free Talk with vision trigger ("what is this?")
- Object tagging in your room with AR highlights
- Word Boxes spelling game with intuitive grab and snap interaction
- Role Play scenarios for real-life conversation training

The tutor can speak any language supported by your selected STT/TTS providers.

---

## 2) Prerequisites

- Unity (version from `ProjectSettings/ProjectVersion.txt`)
- Git + Git LFS
- Meta XR packages (already included in the project)
- API/service access depending on your provider choices:
  - Hugging Face token (LLM/STT and optional detection provider)
  - ElevenLabs API key (if using ElevenLabs TTS)
  - Optional local AllTalk server

---

## 3) Setup after cloning

### 3.1 Clone and open

1. Install Git LFS and run:
  - `git lfs install`
2. Clone the repository.
3. Open the project in Unity Hub.
4. Let Unity finish importing and compiling.

### 3.2 Open the main scene

- Open `Assets/Scenes/Main.unity`.

### 3.3 Create Language Tutor config

1. In the Project window, right-click:
  - `Create > Language Tutor > Language Tutor Config`
2. Name it, for example: `LanguageTutorConfig_Main`.
3. In the Inspector, fill LLM/STT/TTS provider settings and credentials.

### 3.4 Create object detection config (choose one)

You can run object detection with local model inference (YOLO-style) or cloud inference (DETR-style via Hugging Face).

#### Option A: Local model (YOLO via Unity/Meta inference)

1. Duplicate this asset:
  - `Assets/MetaXR/ObjectDetection_UnityInferenceEngine_ProviderProfile1.asset`
2. Rename it (example): `ObjectDetection_LocalYOLO.asset`.
3. Configure:
  - `modelFile` (local model asset)
  - `classLabelsAsset` (e.g. `Assets/MetaXR/class_labels.txt`)
  - input size and detection limits

#### Option B: Hugging Face provider (example: DETR ResNet)

1. Duplicate this asset:
  - `Assets/MetaXR/New Hugging Face Provider.asset`
2. Rename it (example): `ObjectDetection_HF_DETR.asset`.
3. Configure:
  - `apiKey`
  - `modelId` (e.g. `facebook/detr-resnet-101`)
  - `endpoint`

### 3.5 Assign configs in scene

1. Select GameObject `Mila`.
2. On `NPCController`, assign your Language Tutor config to `config`.

3. Select GameObject `[BuildingBlock] Object Detection`.
4. Assign your chosen detection provider/profile in the Object Detection agent setup.

### 3.6 Build & Quick validation

- Build and Run the game.
- Enter Play Mode.
- Press Talk and test one short utterance.
- Confirm subtitles update and Mila replies.
- Confirm object detections are visible/usable.

---

## 4) Feature guide

### 4.1 Multilingual language teacher

- Mila can teach and converse in any language that your configured STT + LLM + TTS pipeline supports.

### 4.2 Free Talk

Purpose: open conversation practice.

Behavior:
- You speak freely.
- Mila answers as a conversational tutor.
- Keyword trigger: saying **"what is this?"** captures a frame and sends it to the LLM.
- With vision-capable setup, Mila can describe or discuss the observed scene/object.

### 4.3 Object Tagging

Purpose: vocabulary learning from real environment context.

Behavior:
- Objects in your room are detected.
- You name/learn them in your target language (current implementation only supports english).
- Matching objects are highlighted.
- Learning flow follows an Anki-like review/reinforcement idea.

### 4.4 Word Boxes game

Purpose: spelling and word formation.

Behavior:
- Place letter boxes into correct slots to form words.
- Includes satisfying snap mechanism and distance grab interaction.
- Vocabulary is sourced from detected room objects.

### 4.5 Role Play

Purpose: real-life scenario practice.

Behavior:
- Mila takes a role (e.g., waiter/shop staff).
- You practice practical conversation turns.
- Current implementation is basic but useful for scenario drills.

---

## 5) UX notes

The app uses Meta XR UI set and is kept **simple**.

Key UX support elements:
- clean panel UI with status indicator
- Slow Mode for easier comprehension
- Replay button for repeating tutor output
- Subtitles for accessibility and retention

---

## 6) Typical user flow

1. Choose language and mode.
2. Press Talk and speak naturally.
3. Read subtitles while listening.
4. Use Slow Mode or Replay when needed.
5. In Object Tagging/Word Boxes, interact with room-based vocabulary.

---

## 7) Troubleshooting

### No tutor response
- Check that `LanguageTutorConfig` is assigned on `Mila > NPCController`.
- Verify provider keys/endpoints.
- Verify app permissions on the Meta Quest
- Check Unity Console for request errors.

### Detection not working
- Ensure detection profile/provider is assigned on `[BuildingBlock] Object Detection`.
- Verify model/provider credentials and compatibility.
- For local models, verify model/labels alignment.

### Subtitles/UI not updating
- Verify scene references on UI/NPC components.
- Check Console for null references.

---
