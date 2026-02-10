using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using LanguageTutor.Data;

namespace LanguageTutor.Services.Vision
{
    /// <summary>
    /// Vision service implementation for image + text chat completions.
    /// </summary>
    public class OpenAIVisionService : IVisionService
    {
        private const int DefaultImageSize = 512;

        private readonly LLMConfig _config;
        private readonly MonoBehaviour _coroutineRunner;

        public OpenAIVisionService(LLMConfig config, MonoBehaviour coroutineRunner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));

            if (string.IsNullOrWhiteSpace(_config.apiKey))
            {
                Debug.LogWarning("[OpenAIVisionService] API key is not set. Please add your API key in the LLMConfig.");
            }
        }

        public string GetModelName()
        {
            string modelName = string.IsNullOrWhiteSpace(_config.visionModelName)
                ? "gpt-4o-mini"
                : _config.visionModelName;

            if (_config.provider == LLMProvider.HuggingFace && !modelName.Contains(":"))
            {
                modelName += ":fireworks-ai";
            }

            return modelName;
        }

        public async Task<string> GenerateResponseAsync(string prompt, Texture2D image, string systemPrompt = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (string.IsNullOrWhiteSpace(_config.apiKey))
                throw new InvalidOperationException("API key is required for vision service. Please set it in the LLMConfig.");

            var tcs = new TaskCompletionSource<string>();
            _coroutineRunner.StartCoroutine(SendRequestCoroutine(prompt, image, systemPrompt, tcs));
            return await tcs.Task;
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_config.apiKey))
                    return false;

                var dummyTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                dummyTexture.SetPixels(new[] { Color.gray, Color.gray, Color.gray, Color.gray });
                dummyTexture.Apply();

                var response = await GenerateResponseAsync("Reply with 'ok'", dummyTexture, "You are a vision assistant.");
                UnityEngine.Object.Destroy(dummyTexture);
                return !string.IsNullOrEmpty(response);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OpenAIVisionService] Availability check failed: {ex.Message}");
                return false;
            }
        }

        private System.Collections.IEnumerator SendRequestCoroutine(
            string prompt,
            Texture2D image,
            string systemPrompt,
            TaskCompletionSource<string> tcs)
        {
            string base64Image = null;
            Texture2D encodedTexture = null;
            bool needsCleanup = false;

            try
            {
                encodedTexture = PrepareTexture(image, DefaultImageSize, DefaultImageSize, out needsCleanup);
                var imageBytes = encodedTexture.EncodeToJPG();
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    tcs.SetException(new Exception("Failed to encode image to JPG. Ensure the texture is readable."));
                    yield break;
                }

                base64Image = Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
                yield break;
            }
            finally
            {
                if (needsCleanup && encodedTexture != null)
                {
                    UnityEngine.Object.Destroy(encodedTexture);
                }
            }

            string payloadJson = BuildPayloadJson(prompt, systemPrompt, base64Image);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);

            string endpointUrl = _config.GetFullUrl();
            using (var webRequest = new UnityWebRequest(endpointUrl, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", "Bearer " + _config.apiKey);
                webRequest.timeout = _config.timeoutSeconds;

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    yield return null;
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<OpenAIChatResponse>(webRequest.downloadHandler.text);
                        if (response == null || response.choices == null || response.choices.Length == 0)
                        {
                            tcs.SetException(new Exception("No choices in response"));
                            yield break;
                        }

                        var content = response.choices[0].message != null
                            ? response.choices[0].message.content
                            : null;

                        if (string.IsNullOrWhiteSpace(content))
                        {
                            tcs.SetException(new Exception("Empty response content"));
                            yield break;
                        }

                        string trimmed = content.Trim();
                        tcs.SetResult(trimmed);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(new Exception($"Failed to parse response: {ex.Message}"));
                    }
                }
                else
                {
                    string errorMsg = $"Vision request failed: {webRequest.error}";
                    if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                    {
                        errorMsg += $"\nResponse: {webRequest.downloadHandler.text}";
                    }
                    tcs.SetException(new Exception(errorMsg));
                }
            }
        }

        private string BuildPayloadJson(string prompt, string systemPrompt, string base64Image)
        {
            string modelName = GetModelName();
            string combinedPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? prompt
                : $"{systemPrompt}\n\n{prompt}";

            string contentJson =
                $"{{\"type\":\"text\",\"text\":\"{EscapeJson(combinedPrompt)}\"}}," +
                $"{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:image/jpeg;base64,{base64Image}\"}}}}";

            return "{" +
                   $"\"model\":\"{EscapeJson(modelName)}\"," +
                   "\"messages\":[" +
                   $"{{\"role\":\"user\",\"content\":[{contentJson}]}}" +
                   "]," +
                   $"\"max_tokens\":{_config.maxTokens}" +
                   "}";
        }

        private Texture2D PrepareTexture(Texture2D source, int targetWidth, int targetHeight, out bool needsCleanup)
        {
            needsCleanup = false;

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

            needsCleanup = true;
            return result;
        }

        private string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
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
