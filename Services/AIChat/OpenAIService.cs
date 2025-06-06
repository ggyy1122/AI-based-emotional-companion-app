using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Linq;
using GameApp.Models.AIChat;
using GameApp.Services.Interfaces;

namespace GameApp.Services.AIChat
{
    public class OpenAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly List<ChatMessage> _conversationHistory;
        private CancellationTokenSource _streamingCts;

        public OpenAIService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {AIConfigSettings.ApiKey}");

            _conversationHistory = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, AIConfigSettings.SystemPrompt)
            };

            _streamingCts = new CancellationTokenSource();
        }

        public async Task<string> GetCompletionAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            try
            {
                // Add user message to history
                _conversationHistory.Add(new ChatMessage(ChatRole.User, userMessage));

                // Create request body
                var requestBody = new
                {
                    model = AIConfigSettings.ModelName,
                    messages = _conversationHistory.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                    temperature = 0.7
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    AIConfigSettings.ApiEndpoint,
                    content,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(jsonResponse);

                string completionText = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? string.Empty;

                // Add assistant response to history
                _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, completionText));

                return completionText;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public async Task StreamCompletionAsync(
            string userMessage,
            Action<string> onPartialResponse,
            CancellationToken cancellationToken = default)
        {
            // Reset cancellation token source
            StopStreaming();
            _streamingCts = new CancellationTokenSource();

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _streamingCts.Token,
                cancellationToken);

            try
            {
                // Add user message to history
                _conversationHistory.Add(new ChatMessage(ChatRole.User, userMessage));

                // Create request body with stream=true for streaming response
                var requestBody = new
                {
                    model = AIConfigSettings.ModelName,
                    messages = _conversationHistory.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                    temperature = 0.7,
                    stream = true
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, AIConfigSettings.ApiEndpoint)
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    linkedCts.Token);

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
                using var reader = new StreamReader(stream);

                StringBuilder fullResponse = new StringBuilder();

                // Process the stream
                while (!reader.EndOfStream && !linkedCts.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line) || line == "data: [DONE]")
                        continue;

                    if (line.StartsWith("data: "))
                    {
                        var json = line.Substring(6); // Remove "data: " prefix

                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            var choices = doc.RootElement.GetProperty("choices");

                            if (choices.GetArrayLength() > 0)
                            {
                                var delta = choices[0].GetProperty("delta");

                                if (delta.TryGetProperty("content", out var contentProperty))
                                {
                                    var content_chunk = contentProperty.GetString() ?? string.Empty;
                                    fullResponse.Append(content_chunk);
                                    onPartialResponse(fullResponse.ToString());
                                }
                            }
                        }
                        catch (JsonException) { /* Skip invalid JSON */ }
                    }
                }

                // Add assistant response to history
                if (fullResponse.Length > 0)
                {
                    _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, fullResponse.ToString()));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                onPartialResponse($"Error: {ex.Message}");
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        public void StopStreaming()
        {
            if (!_streamingCts.IsCancellationRequested)
            {
                _streamingCts.Cancel();
                _streamingCts.Dispose();
                _streamingCts = new CancellationTokenSource();
            }
        }

        /// <summary>
        /// Add a message to conversation history without making API call
        /// Used for rebuilding context from session data
        /// </summary>
        public void AddToConversationHistory(string role, string content)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                _conversationHistory.Add(new ChatMessage(role, content));
            }
        }

        /// <summary>
        /// Clear the conversation history/context while keeping system prompt
        /// </summary>
        public void ClearConversationHistory()
        {
            // Keep only the system prompt (first message)
            var systemPrompt = _conversationHistory.FirstOrDefault(m => m.Role == ChatRole.System);

            _conversationHistory.Clear();

            if (systemPrompt != null)
            {
                _conversationHistory.Add(systemPrompt);
            }
            else
            {
                // Add default system prompt if none exists
                _conversationHistory.Add(new ChatMessage(ChatRole.System, AIConfigSettings.SystemPrompt));
            }
        }
    }
}
