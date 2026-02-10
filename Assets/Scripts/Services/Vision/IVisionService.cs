using System.Threading.Tasks;
using UnityEngine;

namespace LanguageTutor.Services.Vision
{
    /// <summary>
    /// Interface for vision-capable LLM services.
    /// </summary>
    public interface IVisionService
    {
        /// <summary>
        /// Send a prompt and image to the vision model and receive a response.
        /// </summary>
        Task<string> GenerateResponseAsync(string prompt, Texture2D image, string systemPrompt = null);

        /// <summary>
        /// Check if the vision service is available.
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Get the current model name being used.
        /// </summary>
        string GetModelName();
    }
}
