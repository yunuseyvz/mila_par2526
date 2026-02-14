using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if MRUK_INSTALLED
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using Meta.XR.EnvironmentDepth;
#endif

#if MRUK_INSTALLED
[RequireComponent(typeof(ObjectDetectionAgent), typeof(DepthTextureAccess), typeof(EnvironmentDepthManager))]
#endif
public class ObjectQuizHighlighter : MonoBehaviour
{
    [SerializeField] private GameObject highlightPrefab;
    [SerializeField] private bool showBoundingBoxes = true;
    [SerializeField] private Vector3 fallbackBoxScale = new(0.2f, 0.2f, 1f);

    public bool ShowBoundingBoxes
    {
        get => showBoundingBoxes;
        set
        {
            if (showBoundingBoxes == value) return;
            showBoundingBoxes = value;
            foreach (var gameObjectRef in _live)
            {
                if (!gameObjectRef) continue;
                var cache = gameObjectRef.GetComponent<RendererCache>() ?? gameObjectRef.AddComponent<RendererCache>();
                foreach (var rendererRef in cache.Renderers)
                    rendererRef.enabled = value;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate() => ShowBoundingBoxes = showBoundingBoxes;
#endif

    public void SetShowBoundingBoxes(bool value) => ShowBoundingBoxes = value;

    public bool IsHighlighting => _live.Count > 0;
    public string CurrentTargetLabel => _targetLabel;

    private string _targetLabel;

    private readonly List<GameObject> _live = new();
    private readonly Queue<GameObject> _pool = new();

#if MRUK_INSTALLED
    private ObjectDetectionAgent _agent;
    private PassthroughCameraAccess _cam;
    private DepthTextureAccess _depth;
    private int _eyeIdx;

    private struct FrameData
    {
        public Pose Pose;
        public PassthroughCameraAccess.CameraIntrinsics CameraIntrinsics;
        public float[] Depth;
        public Matrix4x4[] ViewProjectionMatrix;
    }

    private FrameData _frame;
#endif

    private void Awake()
    {
#if MRUK_INSTALLED
        _agent = GetComponent<ObjectDetectionAgent>();
        _cam = FindAnyObjectByType<PassthroughCameraAccess>();
        _depth = GetComponent<DepthTextureAccess>();
        if (_cam != null)
            _eyeIdx = _cam.CameraPosition == PassthroughCameraAccess.CameraPositionType.Left ? 0 : 1;
#endif
    }

#if MRUK_INSTALLED
    private void OnEnable()
    {
        _agent.OnBoxesUpdated += HandleBatch;
        _depth.OnDepthTextureUpdateCPU += OnDepth;
    }

    private void OnDisable()
    {
        _agent.OnBoxesUpdated -= HandleBatch;
        _depth.OnDepthTextureUpdateCPU -= OnDepth;
    }

    private void OnDepth(DepthTextureAccess.DepthFrameData depthFrame)
    {
        _frame.Pose = _cam.GetCameraPose();
        _frame.CameraIntrinsics = _cam.Intrinsics;
        _frame.Depth = depthFrame.DepthTexturePixels.ToArray();
        _frame.ViewProjectionMatrix = depthFrame.ViewProjectionMatrix.ToArray();
    }
#endif

    public bool HighlightObject(DetectedObjectRegistry.Entry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.Label))
        {
            Debug.LogWarning("[ObjectQuizHighlighter] Invalid entry provided.");
            return false;
        }

        _targetLabel = entry.Label;
        ShowAtFallbackPose(entry.Position);
        return true;
    }

    public bool HighlightObjectByLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            Debug.LogWarning("[ObjectQuizHighlighter] Cannot highlight - label is empty.");
            return false;
        }

        _targetLabel = label;
        return true;
    }

    public void ClearHighlight()
    {
        _targetLabel = null;

        foreach (var gameObjectRef in _live)
        {
            gameObjectRef.SetActive(false);
            _pool.Enqueue(gameObjectRef);
        }

        _live.Clear();
    }

#if MRUK_INSTALLED
    private void HandleBatch(List<BoxData> batch)
    {
        if (highlightPrefab == null)
        {
            Debug.LogError("[ObjectQuizHighlighter] highlightPrefab is null! Cannot create highlight boxes.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_targetLabel) || batch == null || batch.Count == 0)
            return;

        BoxData? target = null;
        foreach (var detection in batch)
        {
            if (IsTargetMatch(detection.label, _targetLabel))
            {
                target = detection;
                break;
            }
        }

        if (!target.HasValue)
            return;

        var detectionBox = target.Value;
        var xmin = detectionBox.position.x;
        var ymin = detectionBox.position.y;
        var xmax = detectionBox.scale.x;
        var ymax = detectionBox.scale.y;

        if (!TryProject(xmin, ymin, xmax, ymax, out var pos, out var rot, out var scl))
            return;

        ShowAtPose(pos, rot, scl);
    }
#endif

    private void ShowAtFallbackPose(Vector3 position)
    {
        ShowAtPose(position, GetFacingRotation(position), fallbackBoxScale);
    }

    private void ShowAtPose(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (highlightPrefab == null)
        {
            Debug.LogError("[ObjectQuizHighlighter] highlightPrefab is null! Cannot create highlight boxes.");
            return;
        }

        for (var i = _live.Count - 1; i >= 1; i--)
        {
            var staleObject = _live[i];
            staleObject.SetActive(false);
            _pool.Enqueue(staleObject);
            _live.RemoveAt(i);
        }

        var quad = _live.Count > 0
            ? _live[0]
            : (_pool.Count > 0 ? _pool.Dequeue() : Instantiate(highlightPrefab));

        quad.SetActive(true);

        var rendererCache = quad.GetComponent<RendererCache>() ?? quad.AddComponent<RendererCache>();
        foreach (var rendererRef in rendererCache.Renderers)
            rendererRef.enabled = showBoundingBoxes;

        quad.transform.SetPositionAndRotation(position, rotation);
        quad.transform.localScale = scale;
        if (_live.Count == 0)
            _live.Add(quad);
    }

    private Quaternion GetFacingRotation(Vector3 position)
    {
        var viewer = Camera.main != null ? Camera.main.transform : transform;
        var lookDirection = position - viewer.position;

        if (lookDirection.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(lookDirection.normalized);
    }

#if MRUK_INSTALLED
    public bool TryProject(float xmin, float ymin, float xmax, float ymax,
        out Vector3 world, out Quaternion rot, out Vector3 scale)
    {
        world = default;
        rot = default;
        scale = default;

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

    private static bool IsTargetMatch(string detectedLabel, string targetLabel)
    {
        if (string.IsNullOrWhiteSpace(detectedLabel) || string.IsNullOrWhiteSpace(targetLabel))
            return false;

        return detectedLabel.Equals(targetLabel, StringComparison.OrdinalIgnoreCase)
               || detectedLabel.Contains(targetLabel, StringComparison.OrdinalIgnoreCase)
               || targetLabel.Contains(detectedLabel, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RendererCache : MonoBehaviour
    {
        public Renderer[] Renderers;

        private void Awake()
        {
            Renderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
