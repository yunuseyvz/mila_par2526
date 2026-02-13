using System.Collections.Generic;
using UnityEngine;

#if MRUK_INSTALLED
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using Meta.XR.EnvironmentDepth;
#endif

/// <summary>
/// Highlights a specific detected object for the quiz system.
/// Adapted from ObjectDetectionVisualizer to show a single, prominent highlight.
/// </summary>
#if MRUK_INSTALLED
[RequireComponent(typeof(ObjectDetectionAgent), typeof(DepthTextureAccess), typeof(EnvironmentDepthManager))]
#endif
public class ObjectQuizHighlighter : MonoBehaviour
{
    [Header("Highlight Appearance")]
    [SerializeField] private GameObject highlightPrefab;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Vector3 highlightScaleMultiplier = new Vector3(1.2f, 1.2f, 1.0f);
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseMinScale = 0.9f;
    [SerializeField] private float pulseMaxScale = 1.1f;
    [SerializeField] private bool rotateHighlight = true;
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 0f, 90f);
    [SerializeField] private bool forcePointDown = true;
    [SerializeField] private Vector3 pointDownEuler = new Vector3(90f, 0f, 0f);

    private GameObject _currentHighlight;
    private Material _highlightMaterial;
    private Vector3 _baseScale;
    private float _pulseTime;
    private string _currentTargetLabel;
    private DetectedObjectRegistry.Entry _currentTargetEntry;

#if MRUK_INSTALLED
    private ObjectDetectionAgent _agent;
    private PassthroughCameraAccess _cam;
    private DepthTextureAccess _depth;
    private int _eyeIdx;
    private bool _hasValidFrame;

    private struct FrameData
    {
        public Pose Pose;
        public PassthroughCameraAccess.CameraIntrinsics CameraIntrinsics;
        public float[] Depth;
        public Matrix4x4[] ViewProjectionMatrix;
    }

    private FrameData _frame;
#endif

    public bool IsHighlighting => _currentHighlight != null && _currentHighlight.activeSelf;
    public string CurrentTargetLabel => _currentTargetLabel;

    private void Awake()
    {
#if MRUK_INSTALLED
        _agent = GetComponent<ObjectDetectionAgent>();
        _cam = FindAnyObjectByType<PassthroughCameraAccess>();
        _depth = GetComponent<DepthTextureAccess>();
        
        if (_cam != null)
        {
            _eyeIdx = _cam.CameraPosition == PassthroughCameraAccess.CameraPositionType.Left ? 0 : 1;
        }
#endif
    }

#if MRUK_INSTALLED
    private void OnEnable()
    {
        if (_agent != null)
            _agent.OnBoxesUpdated += HandleBoxesUpdated;
        
        if (_depth != null)
            _depth.OnDepthTextureUpdateCPU += OnDepth;
    }

    private void OnDisable()
    {
        if (_agent != null)
            _agent.OnBoxesUpdated -= HandleBoxesUpdated;
        
        if (_depth != null)
            _depth.OnDepthTextureUpdateCPU -= OnDepth;
    }

    private void OnDepth(DepthTextureAccess.DepthFrameData d)
    {
        if (_cam == null) return;
        
        _frame.Pose = _cam.GetCameraPose();
        _frame.CameraIntrinsics = _cam.Intrinsics;
        _frame.Depth = d.DepthTexturePixels.ToArray();
        _frame.ViewProjectionMatrix = d.ViewProjectionMatrix;
        _hasValidFrame = true;
    }

    private void HandleBoxesUpdated(List<BoxData> batch)
    {
        // Only update highlight if we're actively targeting something
        if (string.IsNullOrWhiteSpace(_currentTargetLabel))
            return;

        UpdateHighlightPosition(batch);
    }
#endif

    private void Update()
    {
        if (_currentHighlight != null && _currentHighlight.activeSelf)
        {
            AnimateHighlight();
        }
    }

    /// <summary>
    /// Highlight a specific object by its registry entry.
    /// </summary>
    public bool HighlightObject(DetectedObjectRegistry.Entry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.Label))
        {
            Debug.LogWarning("[ObjectQuizHighlighter] Invalid entry provided.");
            return false;
        }

        _currentTargetEntry = entry;
        _currentTargetLabel = entry.Label;

        // Create or reuse highlight at the entry's stored position
        CreateOrUpdateHighlight(entry.Position, Quaternion.identity, Vector3.one * 0.3f);

        Debug.Log($"[ObjectQuizHighlighter] Highlighting object: {entry.Label} at {entry.Position}");
        return true;
    }

    /// <summary>
    /// Highlight an object by label. Will use detection data if available.
    /// </summary>
    public bool HighlightObjectByLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            Debug.LogWarning("[ObjectQuizHighlighter] Cannot highlight - label is empty.");
            return false;
        }

        _currentTargetLabel = label;
        Debug.Log($"[ObjectQuizHighlighter] Set target label to: {label}, waiting for detection update...");
        return true;
    }

    /// <summary>
    /// Remove the current highlight.
    /// </summary>
    public void ClearHighlight()
    {
        if (_currentHighlight != null)
        {
            _currentHighlight.SetActive(false);
        }

        _currentTargetLabel = null;
        _currentTargetEntry = null;

        Debug.Log("[ObjectQuizHighlighter] Highlight cleared.");
    }

#if MRUK_INSTALLED
    /// <summary>
    /// Update highlight position based on latest detection data.
    /// </summary>
    private void UpdateHighlightPosition(List<BoxData> boxes)
    {
        if (!_hasValidFrame || boxes == null || boxes.Count == 0)
            return;

        // Find the box matching our target label
        BoxData? targetBox = null;
        foreach (var box in boxes)
        {
            if (box.label.Contains(_currentTargetLabel, System.StringComparison.OrdinalIgnoreCase))
            {
                targetBox = box;
                break;
            }
        }

        if (!targetBox.HasValue)
        {
            Debug.Log($"[ObjectQuizHighlighter] Target '{_currentTargetLabel}' not found in current detection batch.");
            return;
        }

        var b = targetBox.Value;
        var xmin = b.position.x;
        var ymin = b.position.y;
        var xmax = b.scale.x;
        var ymax = b.scale.y;

        if (!TryProject(xmin, ymin, xmax, ymax, out var pos, out var rot, out var scl))
        {
            Debug.LogWarning($"[ObjectQuizHighlighter] Failed to project '{_currentTargetLabel}' to 3D space.");
            return;
        }

        // Apply scale multiplier for more prominent highlight
        scl.x *= highlightScaleMultiplier.x;
        scl.y *= highlightScaleMultiplier.y;
        scl.z *= highlightScaleMultiplier.z;

        CreateOrUpdateHighlight(pos, rot, scl);
    }

    /// <summary>
    /// Project 2D bounding box to 3D world space using depth data.
    /// Adapted from ObjectDetectionVisualizer.TryProject
    /// </summary>
    public bool TryProject(float xmin, float ymin, float xmax, float ymax,
        out Vector3 world, out Quaternion rot, out Vector3 scale)
    {
        world = default;
        rot = default;
        scale = default;

        if (_cam == null || !_hasValidFrame)
            return false;

        var px = (xmin + xmax) * 0.5f;
        var py = (ymin + ymax) * 0.5f;

        var dirCam = new Vector3(
            (px - _frame.CameraIntrinsics.PrincipalPoint.x) / _frame.CameraIntrinsics.FocalLength.x,
            -(py - _frame.CameraIntrinsics.PrincipalPoint.y) / _frame.CameraIntrinsics.FocalLength.y,
            1f).normalized;

        var world1M = _frame.Pose.position + _frame.Pose.rotation * dirCam;
        var clip = _frame.ViewProjectionMatrix[_eyeIdx] * new Vector4(world1M.x, world1M.y, world1M.z, 1f);
        if (clip.w <= 0) return false;

        var uv = (new Vector2(clip.x, clip.y) / clip.w) * 0.5f + Vector2.one * 0.5f;
        const int texSize = DepthTextureAccess.TextureSize;
        var sx = Mathf.Clamp((int)(uv.x * texSize), 0, texSize - 1);
        var sy = Mathf.Clamp((int)(uv.y * texSize), 0, texSize - 1);
        var idx = _eyeIdx * texSize * texSize + sy * texSize + sx;
        var d = _frame.Depth[idx];
        if (d <= 0 || d > 20 || float.IsInfinity(d)) return false;

        world = _frame.Pose.position + _frame.Pose.rotation * (dirCam * d);
        rot = Quaternion.LookRotation(world - _frame.Pose.position);
        var w = (xmax - xmin) / _frame.CameraIntrinsics.FocalLength.x * d;
        var h = (ymax - ymin) / _frame.CameraIntrinsics.FocalLength.y * d;
        scale = new Vector3(w, h, 1f);
        return true;
    }
#endif

    /// <summary>
    /// Create or update the highlight GameObject at the specified position.
    /// </summary>
    private void CreateOrUpdateHighlight(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (_currentHighlight == null)
        {
            // Create new highlight
            if (highlightPrefab != null)
            {
                _currentHighlight = Instantiate(highlightPrefab);
            }
            else
            {
                // Create default cube if no prefab provided
                _currentHighlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _currentHighlight.name = "QuizHighlight";

                // Remove collider
                Destroy(_currentHighlight.GetComponent<Collider>());

                // Create glowing material
                _highlightMaterial = new Material(Shader.Find("Standard"));
                _highlightMaterial.EnableKeyword("_EMISSION");
                _highlightMaterial.SetColor("_EmissionColor", highlightColor * 2f);
                _highlightMaterial.SetColor("_Color", new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.5f));

                var renderer = _currentHighlight.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material = _highlightMaterial;
            }

            _pulseTime = 0f;
        }

        _currentHighlight.SetActive(true);
        _currentHighlight.transform.position = position;
        var runtimeRotation = rotation * Quaternion.Euler(rotationOffsetEuler);
        var downRotation = Quaternion.Euler(pointDownEuler) * Quaternion.Euler(rotationOffsetEuler);
        _currentHighlight.transform.rotation = forcePointDown ? downRotation : runtimeRotation;
        _currentHighlight.transform.localScale = scale;
        _baseScale = scale;
    }

    /// <summary>
    /// Animate the highlight with pulsing and rotation.
    /// </summary>
    private void AnimateHighlight()
    {
        if (_currentHighlight == null)
            return;

        _pulseTime += Time.deltaTime * pulseSpeed;

        // Pulsing scale
        float pulseScale = Mathf.Lerp(pulseMinScale, pulseMaxScale, (Mathf.Sin(_pulseTime) + 1f) * 0.5f);
        _currentHighlight.transform.localScale = _baseScale * pulseScale;

        // Rotation
        if (rotateHighlight)
        {
            _currentHighlight.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }

        // Pulse emission intensity if we have a material
        if (_highlightMaterial != null)
        {
            float intensity = Mathf.Lerp(1.5f, 3f, (Mathf.Sin(_pulseTime * 1.5f) + 1f) * 0.5f);
            _highlightMaterial.SetColor("_EmissionColor", highlightColor * intensity);
        }
    }

    private void OnDestroy()
    {
        if (_currentHighlight != null)
        {
            Destroy(_currentHighlight);
        }

        if (_highlightMaterial != null)
        {
            Destroy(_highlightMaterial);
        }
    }
}
