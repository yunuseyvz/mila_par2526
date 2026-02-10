using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using LanguageTutor.Data;

namespace LanguageTutor.Services.Vision {

    [CreateAssetMenu(fileName = "VisionPromptRunner", menuName = "Language Tutor/Vision Prompt Runner", order = 10)]
    public class VisionPromptRunnerAsset : ScriptableObject
    {
        [Header("Config")]
        [SerializeField] private LLMConfig llmConfig;
        [SerializeField, TextArea(2, 6)] private string prompt = "What object am I pointing at?";
        [SerializeField, TextArea(2, 6)] private string systemPrompt = "You are a vision assistant.";

        [Header("Image")]
        [SerializeField] private string imageFilePath;

        [Header("Output")]
        [SerializeField] private bool saveResponseToDesktop = true;

        public async Task RunAsync()
        {
            if (llmConfig == null)
            {
                Debug.LogError("[VisionPromptRunnerAsset] LLMConfig is not assigned.");
                return;
            }

            if (string.IsNullOrWhiteSpace(imageFilePath) || !File.Exists(imageFilePath))
            {
                Debug.LogError("[VisionPromptRunnerAsset] Image file path is invalid.");
                return;
            }

            byte[] bytes = File.ReadAllBytes(imageFilePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                DestroyImmediate(texture);
                Debug.LogError("[VisionPromptRunnerAsset] Failed to load image bytes into Texture2D.");
                return;
            }

            try
            {
                string response = await SendRequestAsync(prompt, systemPrompt, texture, llmConfig);
                Debug.Log($"[VisionPromptRunnerAsset] Response: {response}");

                if (saveResponseToDesktop)
                {
                    string folder = GetDesktopFolder();
                    Directory.CreateDirectory(folder);
                    string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                    string path = Path.Combine(folder, $"vlm_test_response_{stamp}.txt");
                    File.WriteAllText(path, response ?? string.Empty);
                    Debug.Log($"[VisionPromptRunnerAsset] Saved response to: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VisionPromptRunnerAsset] Request failed: {ex.Message}");
            }
            finally
            {
                DestroyImmediate(texture);
            }
        }

        private static async Task<string> SendRequestAsync(string promptText, string systemPromptText, Texture2D image, LLMConfig config)
        {
            if (string.IsNullOrWhiteSpace(promptText))
                throw new ArgumentException("Prompt cannot be empty", nameof(promptText));

            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (string.IsNullOrWhiteSpace(config.apiKey))
                throw new InvalidOperationException("API key is required for vision service. Please set it in the LLMConfig.");

            string payloadJson = BuildPayloadJson(promptText, systemPromptText, image, config);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);

            string endpointUrl = config.GetFullUrl();
            Debug.Log("[VisionPromptRunnerAsset] Sending VLM request...");
            using (var webRequest = new UnityWebRequest(endpointUrl, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Bearer " + config.apiKey);
                webRequest.timeout = config.timeoutSeconds;

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    Debug.Log("[VisionPromptRunnerAsset] AAA");
                    await Task.Yield();
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[VisionPromptRunnerAsset] AAAAAA");
                    var response = JsonUtility.FromJson<OpenAIChatResponse>(webRequest.downloadHandler.text);
                    if (response == null || response.choices == null || response.choices.Length == 0)
                    {
                        throw new Exception("No choices in response");
                    }

                    var content = response.choices[0].message != null
                        ? response.choices[0].message.content
                        : null;

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        throw new Exception("Empty response content");
                    }

                    string trimmed = content.Trim();
                    Debug.Log("[VisionPromptRunnerAsset] VLM request succeeded.");
                    return trimmed;
                }

                Debug.Log($"[VisionPromptRunnerAsset] Vision request failed: {webRequest.error}");
                string errorMsg = $"Vision request failed: {webRequest.error}";
                if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                {
                    errorMsg += $"\nResponse: {webRequest.downloadHandler.text}";
                }
                Debug.LogError("[VisionPromptRunnerAsset] VLM request failed.");
                throw new Exception(errorMsg);
            }
        }

        private static string BuildPayloadJson(string promptText, string systemPromptText, Texture2D image, LLMConfig config)
        {
            string modelName = string.IsNullOrWhiteSpace(config.visionModelName) ? "gpt-4o-mini" : config.visionModelName;
            if (config.provider == LLMProvider.HuggingFace && !modelName.Contains(":"))
            {
                modelName += ":fireworks-ai";
            }
            string combinedPrompt = string.IsNullOrWhiteSpace(systemPromptText)
                ? promptText
                : $"{systemPromptText}\n\n{promptText}";

            string base64Image = Convert.ToBase64String(PrepareTexture(image).EncodeToJPG());
            Debug.Log(base64Image);
            string contentJson =
                $"{{\"type\":\"text\",\"text\":\"{EscapeJson(combinedPrompt)}\"}}," +
                $"{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:image/jpeg;base64,{base64Image}\"}}}}";

            return "{" +
                   $"\"model\":\"{EscapeJson(modelName)}\"," +
                   "\"messages\":[" +
                   $"{{\"role\":\"user\",\"content\":[{contentJson}]}}" +
                   "]," +
                   $"\"max_tokens\":{config.maxTokens}" +
                   "}";
        }

        private static Texture2D PrepareTexture(Texture2D source)
        {
            const int targetWidth = 512;
            const int targetHeight = 512;

            if (source.width == targetWidth && source.height == targetHeight && source.format == TextureFormat.RGBA32)
            {
                return source;
            }

            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            rt.filterMode = FilterMode.Bilinear;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
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

        [Serializable]
        private class OpenAIChatResponse
        {
            public OpenAIChatChoice[] choices;
        }

        [Serializable]
        private class OpenAIChatChoice
        {
            public OpenAIChatMessage message;
        }

        [Serializable]
        private class OpenAIChatMessage
        {
            public string content;
        }
    }
}
