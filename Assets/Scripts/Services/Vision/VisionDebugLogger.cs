using System;
using System.IO;
using UnityEngine;

namespace LanguageTutor.Services.Vision
{
    /// <summary>
    /// Debug helpers for saving VLM artifacts.
    /// </summary>
    public static class VisionDebugLogger
    {
        public static string SaveFrame(Texture2D image, string prefix = "vlm_frame")
        {
            if (image == null)
                return null;

            string folder = GetDesktopFolder();
            Directory.CreateDirectory(folder);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string path = Path.Combine(folder, $"{prefix}_{stamp}.png");

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
                : Path.Combine(desktop, "VLM_Debug");
        }
    }
}
