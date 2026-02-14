using UnityEngine;
using UnityEngine.UI;
using LanguageTutor.Core;

public class VRButtonInput : MonoBehaviour
{
    [SerializeField] private Button talkButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private NPCController npcController;

    private const OVRInput.Button TalkInputButton = OVRInput.Button.One;
    private const OVRInput.Button StopInputButton = OVRInput.Button.Two;
    private const OVRInput.Button ToggleTtsInputButton = OVRInput.Button.Three;

    private void Awake()
    {
        if (npcController == null)
        {
            npcController = FindObjectOfType<NPCController>();
        }
    }

    void Update()
    {
        // A button on right controller (push-to-talk: press to start, release to stop)
        if (OVRInput.GetDown(TalkInputButton))
        {
            InvokeTalkButton();
        }

        if (OVRInput.GetUp(TalkInputButton))
        {
            InvokeTalkButton();
        }

        // B button on right controller
        if (OVRInput.GetDown(StopInputButton))
        {
            if (stopButton != null)
            {
                stopButton.onClick.Invoke();
            }
            else
            {
                npcController?.StopCurrentSpeech();
            }
        }

        // X button on left controller - toggle TTS provider (ElevenLabs/AllTalk)
        if (OVRInput.GetDown(ToggleTtsInputButton))
        {
            npcController?.ToggleTTSProvider();
        }
    }

    private void InvokeTalkButton()
    {
        if (talkButton != null && talkButton.IsInteractable())
        {
            talkButton.onClick.Invoke();
        }
    }
}