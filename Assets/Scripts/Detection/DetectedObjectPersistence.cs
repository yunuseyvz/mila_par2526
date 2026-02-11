using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DetectedObjectPersistence : MonoBehaviour
{
    [SerializeField] private DetectedObjectRegistry registry;
    [SerializeField] private string fileName = "detected_registry.json";
    [SerializeField] private bool autoLoadOnStart = true;
    [SerializeField] private bool autoSaveOnPause = true;
    [SerializeField] private bool autoSaveOnQuit = true;
    [SerializeField] private TextMeshProUGUI statusText;

    private string message;
    private float messageUntil;
    private ObjectDetectionListRecorder _recorder;

    private void Start()
    {
        Debug.Log("[DetectedObjectPersistence] Start() running");
        Debug.LogError("[DetectedObjectPersistence] START CALLED - USING ERROR LEVEL TO ENSURE VISIBILITY");

        // Ensure there's a visible status text. Create a simple Canvas+TMP text at runtime if none assigned.
        if (statusText == null)
        {
            Debug.LogError("[DetectedObjectPersistence] StatusText is null, creating default UI");
            CreateDefaultStatusUI();
            Debug.LogError("[DetectedObjectPersistence] Default UI created, statusText is now: " + (statusText == null ? "STILL NULL" : "SET"));
        }

        if (registry == null)
        {
            Debug.LogWarning("[DetectedObjectPersistence] Registry not assigned in inspector.");
            return;
        }

        if (autoLoadOnStart)
        {
            var ok = TryInvokeLoad(registry, fileName);
            ShowMessage(ok ? "Registry loaded" : "No registry file to load");
        }

        // Subscribe to the recorder event for immediate updates
        _recorder = FindObjectOfType<ObjectDetectionListRecorder>();
        Debug.LogError("[DetectedObjectPersistence] ObjectDetectionListRecorder found: " + (_recorder == null ? "NOT FOUND" : "FOUND"));
        if (_recorder != null)
        {
            _recorder.OnDetectionsThisFrame += HandleDetectionsSnapshot;
            Debug.LogError("[DetectedObjectPersistence] Subscribed to OnDetectionsThisFrame");
        }
    }

    public void Save()
    {
        if (registry == null)
        {
            ShowMessage("No registry assigned");
            return;
        }

        var path = TryInvokeSave(registry, fileName);
        ShowMessage(path != null ? $"Saved to {path}" : "Save failed");
    }

    public void Load()
    {
        if (registry == null)
        {
            ShowMessage("No registry assigned");
            return;
        }

        var ok = TryInvokeLoad(registry, fileName);
        ShowMessage(ok ? "Loaded registry" : "Load failed or file missing");
    }

    public void Clear()
    {
        if (registry == null)
        {
            ShowMessage("No registry assigned");
            return;
        }

        registry.Clear();
        ShowMessage($"Registry cleared");
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause && autoSaveOnPause)
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        if (autoSaveOnQuit)
        {
            Save();
        }
    }

    private void OnDestroy()
    {
        // Try to save as a last-resort when the component is destroyed.
        Save();
        if (_recorder != null)
        {
            _recorder.OnDetectionsThisFrame -= HandleDetectionsSnapshot;
        }
    }

    private void HandleDetectionsSnapshot(System.Collections.Generic.IReadOnlyList<DetectedObjectRegistry.Entry> snapshot)
    {
        // Update UI immediately with snapshot count
        if (statusText != null)
        {
            statusText.text = $"Detected Objects: {snapshot?.Count ?? 0}";
        }
    }

    // Reflection helpers: use these to call optional SaveToFile/LoadFromFile methods on the registry
    private bool TryInvokeLoad(DetectedObjectRegistry reg, string fileName)
    {
        if (reg == null) return false;
        try
        {
            var mi = reg.GetType().GetMethod("LoadFromFile");
            if (mi == null) return false;
            var result = mi.Invoke(reg, new object[] { fileName });
            return result is bool b && b;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DetectedObjectPersistence] Reflection load failed: {e.Message}");
            return false;
        }
    }

    private string TryInvokeSave(DetectedObjectRegistry reg, string fileName)
    {
        if (reg == null) return null;
        try
        {
            var mi = reg.GetType().GetMethod("SaveToFile");
            if (mi == null) return null;
            var result = mi.Invoke(reg, new object[] { fileName });
            return result as string;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DetectedObjectPersistence] Reflection save failed: {e.Message}");
            return null;
        }
    }

    private void CreateDefaultStatusUI()
    {
        try
        {
            Debug.LogError("[DetectedObjectPersistence] CreateDefaultStatusUI() called");
            // Create Canvas
            var canvasGO = new GameObject("DetectedUI_Canvas");
            Debug.LogError("[DetectedObjectPersistence] Canvas created");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Create Text (TMP)
            var textGO = new GameObject("StatusText_TMP");
            textGO.transform.SetParent(canvasGO.transform, false);
            var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.fontSize = 36;
            tmp.color = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
            tmp.text = "Detected Objects: 0";

            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(10, -10);
            rt.sizeDelta = new Vector2(600, 80);

            statusText = tmp;
            Debug.LogError("[DetectedObjectPersistence] Default UI created successfully");
        }
        catch (Exception e)
        {
            Debug.LogError("[DetectedObjectPersistence] Failed to create default UI: " + e.Message);
        }
    }

    private void ShowMessage(string text, float duration = 3f)
    {
        message = text;
        messageUntil = Time.realtimeSinceStartup + duration;
        Debug.Log("[DetectedObjectPersistence] " + text);

        if (statusText != null)
        {
            statusText.text = text;
        }
    }

    private void Update()
    {
        // Show real-time registry entry count
        if (statusText != null && registry != null && Time.realtimeSinceStartup > messageUntil)
        {
            statusText.text = $"Detected Objects: {registry.Entries.Count}";
        }
    }

    private void OnGUI()
    {
        // Always show a fallback box in case something failed
        var statusLine = "Detected: ";
        if (registry != null)
        {
            statusLine += registry.Entries.Count.ToString();
        }
        else
        {
            statusLine += "NO REGISTRY";
        }

        if (Time.realtimeSinceStartup <= messageUntil && !string.IsNullOrEmpty(message))
        {
            statusLine = message;
        }

        var style = new GUIStyle(GUI.skin.box) { fontSize = 20, normal = { textColor = Color.white } };
        var rect = new Rect(10, 10, Screen.width - 20, 60);
        GUI.Box(rect, statusLine, style);
    }
}
