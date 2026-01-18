# PAR_WS2526 - AR Language Tutor

An **AR-based language learning application** powered by **LLM (Large Language Model)** technology for immersive conversational practice.

## Project Overview

This Unity project combines:
- **Augmented Reality (Meta Quest)** for immersive learning environments
- **Ollama/Llama3** for intelligent conversational AI
- **Whisper** for accurate speech-to-text recognition
- **AllTalk TTS** for natural text-to-speech synthesis

The application provides an interactive language tutor NPC that can engage in conversations, correct grammar, teach vocabulary, and provide pronunciation feedback.

## Getting Started

This project uses **Unity** and **Git Large File Storage (LFS)** to manage large binary assets (textures, models, audio, etc.).

### Prerequisites

Before cloning the repository, ensure you have the following installed:

1.  **Git**: [Download Git](https://git-scm.com/downloads)
2.  **Git LFS**: [Download Git LFS](https://git-lfs.com/)
3.  **Unity Hub & Editor**: Install the version specified in `ProjectSettings/ProjectVersion.txt` (or the latest stable release if not specified).
4.  **Ollama with Llama3**: [Download Ollama](https://ollama.ai/) and run `ollama pull llama3`
5.  **AllTalk TTS**: [AllTalk TTS](https://github.com/erew123/alltalk_tts/tree/alltalkbeta)

### Installation

1.  **Install Git LFS**:
    Open your terminal or command prompt and run:
    ```bash
    git lfs install
    ```

2.  **Clone the Repository**:
    ```bash
    git clone <repository-url>
    cd PAR_WS2526
    ```

3.  **Pull LFS Assets**:

### Setting Up the Application

**For detailed setup instructions, see:**
- [**SETUP_GUIDE.md**](Assets/_Project/SETUP_GUIDE.md) - Step-by-step configuration guide
- [**ARCHITECTURE.md**](Assets/_Project/ARCHITECTURE.md) - Complete architecture documentation

**Quick Start:**
1. Create configuration assets: `Assets → Create → Language Tutor → [Config Type]`
2. Start external services:
   - Ollama: `ollama run llama3`
   - AllTalk TTS: Run server on port 7851
3. Configure NPCController and NPCView in your scene
4. Press Play and click "Talk" to begin conversation
   Project Architecture

The codebase has been refactored into a clean, maintainable architecture:

```
Assets/_Project/Scripts/
├── Core/          # Main application logic (NPCController, ConversationPipeline)
├── Services/      # Interface-based services (LLM, TTS, STT)
├── Actions/       # Generic LLM action system (Command Pattern)
├── Data/          # Configuration ScriptableObjects
├── UI/            # User interface components
└── Utilities/     # Helper utilities
```

**Key Features:**
- ✅ **Service-Oriented Architecture** with dependency injection
- ✅ **Generic LLM Action System** for extensible AI behaviors
- ✅ **Configuration via ScriptableObjects** (no hardcoded values)
- ✅ **Multi-turn conversation history** for context-aware responses
- ✅ **Event-driven communication** for loose coupling
- ✅ **Automatic retry logic** with error handling

## Available Features

- **Chat Mode**: General conversation practice
- **Grammar Check**: Automatic grammar correction with explanations
- **Vocabulary Teaching**: Word definitions and usage examples
- **Conversation Practice**: Scenario-based dialogue (ordering food, asking directions, etc.)
- **Multi-language Support**: Configurable target language
- **Adaptive Difficulty**: CEFR levels (A1-C2)
- **Speech Recognition**: Real-time transcription with confidence scoring
- **Natural TTS**: Human-like speech synthesis with audio caching

## Contributing

When adding new large binary files, ensure they are tracked by LFS. You can add new file types using:
```bash
git lfs track "*.extension"
```
This will update the `.gitattributes` file.


## Documentation

- **[SETUP_GUIDE.md](Assets/_Project/SETUP_GUIDE.md)** - Complete setup instructions
- **[ARCHITECTURE.md](Assets/_Project/ARCHITECTURE.md)** - Architecture overview and extension guide

## Git LFS Configuration

This repository is configured to track the following file types with Git LFS:
-   Images: `.psd`, `.png`, `.jpg`, `.tga`
-   Audio: `.wav`, `.mp3`, `.ogg`
-   Models: `.fbx`
-   Video: `.mp4`, `.avi`

To verify LFS is working, you can run:
```bash
git lfs ls-files
```
