using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameApp.Services.Interfaces
{
    public interface IAIService
    {
        /// <summary>
        /// Get a complete response from the AI service
        /// </summary>
        Task<string> GetCompletionAsync(string userMessage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream a response from the AI service with incremental updates
        /// </summary>
        Task StreamCompletionAsync(
            string userMessage,
            Action<string> onPartialResponse,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop any ongoing streaming response
        /// </summary>
        void StopStreaming();
    }
}
