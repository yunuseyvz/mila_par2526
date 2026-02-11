using UnityEngine;
using UnityEngine.UI;

public class DetectionUISetupHelper : MonoBehaviour
{
    [SerializeField] private DetectedObjectPersistence persistenceComponent;

    private void Start()
    {
        // If no Canvas exists, this helper logs a warning but doesn't auto-create (you should set it up manually)
        if (persistenceComponent == null)
        {
            persistenceComponent = FindObjectOfType<DetectedObjectPersistence>();
        }

        if (persistenceComponent == null)
        {
            Debug.LogWarning("[DetectionUISetupHelper] No DetectedObjectPersistence found in scene. Please attach manually.");
        }
    }

    public virtual void OnSaveButtonPressed()
    {
        if (persistenceComponent != null)
        {
            persistenceComponent.Save();
        }
    }

    public virtual void OnLoadButtonPressed()
    {
        if (persistenceComponent != null)
        {
            persistenceComponent.Load();
        }
    }

    public virtual void OnClearButtonPressed()
    {
        if (persistenceComponent != null)
        {
            persistenceComponent.Clear();
        }
    }
}
