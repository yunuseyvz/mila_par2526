using UnityEngine;

public class HealthCheck : MonoBehaviour
{
    private void Awake()
    {
        Debug.LogError("========== HEALTH CHECK: Awake called ==========");
    }

    private void Start()
    {
        Debug.LogError("========== HEALTH CHECK: Start called ==========");
        Debug.LogError("Scene name: " + gameObject.scene.name);
        Debug.LogError("GameObject active: " + gameObject.activeInHierarchy);
        Debug.LogError("Script enabled: " + enabled);
    }

    private void OnEnable()
    {
        Debug.LogError("========== HEALTH CHECK: OnEnable called ==========");
    }

    private void Update()
    {
        // Log once per 5 seconds
        if (Time.frameCount % 300 == 0)
        {
            Debug.LogError("========== HEALTH CHECK: Running (frame " + Time.frameCount + ") ==========");
        }
    }
}
