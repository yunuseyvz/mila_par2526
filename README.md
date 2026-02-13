# Mila AR - Your personal language tutor

<img src="Assets/Resources/app_icon.png" alt="Mila AR App Icon" width="180" />

**Course:** Praktikum Augmented Reality, LMU Munich, WS25/26

**Group Members:**
- Entoni Jombi (@saitamaisreal)
- Anna Maria Lödige (@annamaria-loe)
- Kevin Kafexhi (@Kafexhi)
- Yunus Emre Yavuz (@yunuseyvz)

Mila AR is an augmented reality language learning app built in Unity. It provides spoken conversational practice with an AI tutor by combining speech recognition, LLM-based response generation, and natural speech synthesis.

## Tech Stack

- **Engine:** Unity 6000.3.0f1
- **STT:** Hugging Face Inference Providers with Whisper
- **LLM:** Hugging Face Inference Providers with Gemma 27B
- **TTS:** ElevenLabs or AllTalk

## Project Focus

- AR-first language tutoring experience
- Interactive spoken dialogue for practice and feedback
- Vision-aware tutoring using YOLOv12 and Gemma vision capabilities for room-context understanding
- Modular pipeline for Speech-to-Text → LLM → Text-to-Speech

## Getting Started

### Prerequisites

1. **Git**: https://git-scm.com/downloads
2. **Git LFS**: https://git-lfs.com/
3. **Unity Hub + Unity Editor** (version from `ProjectSettings/ProjectVersion.txt`)
4. Access/API credentials for:
    - Hugging Face Inference Providers (Whisper + Gemma 27B)
    - ElevenLabs (if using ElevenLabs TTS)
    - AllTalk (if using local/self-hosted TTS)

### Setup

1. Install Git LFS:
    ```bash
    git lfs install
    ```
2. Clone the repository:
    ```bash
    git clone <repository-url>
    cd PAR_WS2526
    ```
3. Open the project in Unity.
4. Configure provider credentials/endpoints in the project config.
5. Start Play Mode and test the voice interaction loop.

## Git LFS

Large binary assets are managed with Git LFS.

- Track additional file types if needed:
  ```bash
  git lfs track "*.extension"
  ```
- Verify tracked assets:
  ```bash
  git lfs ls-files
  ```
