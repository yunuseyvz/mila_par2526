using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Meta.XR;

namespace LanguageTutor.Core
{
    /// <summary>
    /// Captures a single passthrough frame and returns a JPEG data URL.
    /// </summary>
    public class PassthroughFrameCapture : MonoBehaviour
    {
        [SerializeField] private int targetSize = 512;
        [SerializeField] private PassthroughCameraAccess cameraAccess;

        public Task<string> CaptureFrameDataUrlAsync(int overrideTargetSize = -1)
        {
            var tcs = new TaskCompletionSource<string>();
            StartCoroutine(CaptureFrameCoroutine(overrideTargetSize, tcs));
            return tcs.Task;
        }

        private IEnumerator CaptureFrameCoroutine(int overrideTargetSize, TaskCompletionSource<string> tcs)
        {
            if (cameraAccess == null)
            {
                cameraAccess = FindObjectOfType<PassthroughCameraAccess>(true);
            }

            if (cameraAccess == null)
            {
                tcs.SetException(new InvalidOperationException("PassthroughCameraAccess not found in scene."));
                yield break;
            }

            if (!cameraAccess.enabled)
            {
                cameraAccess.enabled = true;
            }

            float startTime = Time.time;
            while (!cameraAccess.IsPlaying && Time.time - startTime < 2.0f)
            {
                yield return null;
            }

            if (!cameraAccess.IsPlaying)
            {
                tcs.SetException(new InvalidOperationException("PassthroughCameraAccess is not playing."));
                yield break;
            }

            float updatedStart = Time.time;
            while (!cameraAccess.IsUpdatedThisFrame && Time.time - updatedStart < 0.5f)
            {
                yield return null;
            }

            yield return new WaitForEndOfFrame();

            var resolution = cameraAccess.CurrentResolution;
            if (resolution.x <= 0 || resolution.y <= 0)
            {
                tcs.SetException(new InvalidOperationException("PassthroughCameraAccess resolution is invalid."));
                yield break;
            }

            Texture2D source = null;
            Texture2D resized = null;
            try
            {
                var colors = cameraAccess.GetColors();
                source = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
                source.SetPixelData(colors, 0);
                source.Apply(false, false);

                int size = overrideTargetSize > 0 ? overrideTargetSize : targetSize;
                resized = ResizeTexture(source, size);

                if (!ReferenceEquals(resized, source))
                {
                    Destroy(source);
                    source = null;
                }

                byte[] jpg = resized.EncodeToJPG();
                Destroy(resized);
                resized = null;

                string dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(jpg);
                tcs.SetResult(dataUrl);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            finally
            {
                if (source != null)
                {
                    Destroy(source);
                }
                if (resized != null)
                {
                    Destroy(resized);
                }
            }
        }

        private Texture2D ResizeTexture(Texture2D source, int size)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (size <= 0 || (source.width == size && source.height == size))
            {
                return source;
            }

            var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply(false, false);

            RenderTexture.active = prev;
            rt.Release();
            Destroy(rt);

            return tex;
        }
    }
}
