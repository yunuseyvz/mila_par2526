using UnityEngine;
using UnityEngine.UI;

public class VRButtonInput : MonoBehaviour
{
    [SerializeField] private Button talkButton;

    void Update()
    {
        // A button on right controller
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            talkButton?.onClick.Invoke();
        }
    }
}