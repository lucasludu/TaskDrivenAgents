using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TaskDrivenAgent.Services
{
    public class OpenAiLlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        public string Model { get; set; }
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; }

        public OpenAiLlmService(string apiKey, string baseUrl = "https://api.openai.com/v1/", string model = "gpt-4o-mini")
        {
            Model = model;
            ApiKey = apiKey;
            BaseUrl = baseUrl;
            
            // Allow bypassing SSL checks for local endpoints if needed
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            _httpClient = new HttpClient(handler);
        }

        public async Task<string> GetCompletionAsync(List<ChatMessage> messages)
        {
            var requestBody = new ChatCompletionRequest
            {
                Model = Model,
                Messages = messages,
                Temperature = 0.1f // Lower temperature for more deterministic tool calling and reasoning
            };

            int maxRetries = 4;
            int delaySeconds = 6;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var requestUrl = BaseUrl.EndsWith("/") ? BaseUrl + "chat/completions" : BaseUrl + "/chat/completions";
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                
                request.Content = JsonContent.Create(requestBody);
                
                if (!string.IsNullOrWhiteSpace(ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
                }

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
                    return result?.Choices?[0]?.Message?.Content ?? "";
                }

                var statusCode = (int)response.StatusCode;
                if ((statusCode == 429 || statusCode == 503) && attempt < maxRetries)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n[LLM] Límite de cuota o servicio no disponible (HTTP {statusCode}). Reintentando en {delaySeconds}s (Intento {attempt}/{maxRetries - 1})...");
                    Console.ResetColor();
                    
                    await Task.Delay(delaySeconds * 1000);
                    delaySeconds *= 2; // Exponential backoff
                    continue;
                }

                var errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"LLM API Request failed with status code {response.StatusCode}. Details: {errorText}");
            }

            throw new Exception("LLM API Request failed after multiple retries.");
        }

        private class ChatCompletionRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = "";

            [JsonPropertyName("messages")]
            public List<ChatMessage> Messages { get; set; } = new();

            [JsonPropertyName("temperature")]
            public float Temperature { get; set; } = 0.1f;
        }

        private class ChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<Choice>? Choices { get; set; }
        }

        private class Choice
        {
            [JsonPropertyName("message")]
            public ResponseMessage? Message { get; set; }
        }

        private class ResponseMessage
        {
            [JsonPropertyName("content")]
            public string Content { get; set; } = "";
        }
    }
}
