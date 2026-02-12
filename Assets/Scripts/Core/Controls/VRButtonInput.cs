using UnityEngine;
using UnityEngine.UI;
using LanguageTutor.Core;

public class VRButtonInput : MonoBehaviour
{
    [SerializeField] private Button talkButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private NPCController npcController;

    private void Awake()
    {
        if (npcController == null)
        {
            npcController = FindObjectOfType<NPCController>();
        }
    }

    void Update()
    {
        // A button on right controller
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            talkButton?.onClick.Invoke();
        }

        // B button on right controller
        if (OVRInput.GetDown(OVRInput.Button.Two))
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
    }
}