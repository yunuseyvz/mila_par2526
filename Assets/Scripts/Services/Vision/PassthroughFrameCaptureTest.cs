using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Meta.XR;

namespace LanguageTutor.Services.Vision
{
    /// <summary>
    /// Captures a single passthrough frame and saves it to Desktop/VLM_.
    /// </summary>
    [ExecuteAlways]
    public class PassthroughFrameCaptureTest : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess passthroughCameraAccess;
        [SerializeField] private float captureDelaySeconds = 0.5f;
        [SerializeField] private Button captureButton;

        private void Start()
        {
            if (captureButton != null)
            {
                captureButton.onClick.AddListener(CaptureFrame);
            }
        }

        private void OnDestroy()
        {
            if (captureButton != null)
            {
                captureButton.onClick.RemoveListener(CaptureFrame);
            }
        }

        [ContextMenu("Capture Passthrough Frame")]
        public void CaptureFrame()
        {
            Debug.Log("[PassthroughFrameCaptureTest] CaptureFrame called");
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PassthroughFrameCaptureTest] Enter Play Mode to capture a passthrough frame.");
                return;
            }
            StartCoroutine(CaptureFrameCoroutine());
        }

        private System.Collections.IEnumerator CaptureFrameCoroutine()
        {
            Debug.Log("[PassthroughFrameCaptureTest] Capture coroutine started");
            if (!TryResolveCameraAccess(out var access) || !access.IsPlaying)
            {
                Debug.Log("[PassthroughFrameCaptureTest] PassthroughCameraAccess not available or not playing.");
                yield break;
            }

            Debug.Log($"[PassthroughFrameCaptureTest] Passthrough ready. Resolution: {access.CurrentResolution}");

            if (captureDelaySeconds > 0f)
            {
                Debug.Log($"[PassthroughFrameCaptureTest] Waiting {captureDelaySeconds:F2}s before capture...");
                yield return new WaitForSeconds(captureDelaySeconds);
            }

            Debug.Log("[PassthroughFrameCaptureTest] Waiting for end of frame...");
            yield return new WaitForEndOfFrame();

            var texture = CapturePassthroughFrame(access);
            if (texture == null)
            {
                Debug.LogError("[PassthroughFrameCaptureTest] Failed to capture frame.");
                yield break;
            }

            Debug.Log("[PassthroughFrameCaptureTest] Frame captured, saving...");
            string path = SaveFrame(texture);
            Destroy(texture);

            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogError("[PassthroughFrameCaptureTest] Failed to save frame.");
                yield break;
            }

            Debug.Log($"[PassthroughFrameCaptureTest] Saved frame to: {path}");
        }

        private bool TryResolveCameraAccess(out PassthroughCameraAccess access)
        {
            Debug.Log("[PassthroughFrameCaptureTest] AAA");
            access = passthroughCameraAccess ? passthroughCameraAccess : FindAnyObjectByType<PassthroughCameraAccess>();
            passthroughCameraAccess = access;
            Debug.Log("[PassthroughFrameCaptureTest] BBB");
            Debug.Log($"[PassthroughFrameCaptureTest] {access}");
            return access != null;
        }

        private static Texture2D CapturePassthroughFrame(PassthroughCameraAccess access)
        {
            var resolution = access.CurrentResolution;
            if (resolution == Vector2Int.zero)
                return null;

            var colors = access.GetColors();
            if (!colors.IsCreated)
                return null;

            var texture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
            texture.SetPixelData(colors, 0);
            texture.Apply();
            return texture;
        }

        private static string SaveFrame(Texture2D image)
        {
            string folder = GetDesktopFolder();
            Directory.CreateDirectory(folder);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string path = Path.Combine(folder, $"frame_{stamp}.png");

            byte[] pngBytes = image.EncodeToPNG();
            if (pngBytes == null || pngBytes.Length == 0)
                return null;

            File.WriteAllBytes(path, pngBytes);
            return path;
        }

        private static string GetDesktopFolder()
        {
            string desktop = null;
            try
            {
                desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }
            catch
            {
                desktop = null;
            }

            return string.IsNullOrWhiteSpace(desktop)
                ? Application.persistentDataPath
                : Path.Combine(desktop, "VLM_");
        }
    }
}
