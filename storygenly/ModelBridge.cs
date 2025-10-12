using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StoryGenly
{
    public class ModelBridge
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "http://localhost:11434/api/";
        private string _model = "dolphin3:latest";

        public ModelBridge(string baseUrl, string? model = null)
        {
            _httpClient = new HttpClient();
            _baseUrl = baseUrl;
            if (!string.IsNullOrWhiteSpace(model))
                _model = model;
        }

        // Generate a completion (POST /api/generate)
        public async Task<string> GenerateAsync(string prompt, string? model = null, object? options = null, string? format = null, string? suffix = null, bool stream = false)
        {
            var requestBody = new Dictionary<string, object?>
            {
                ["model"] = model ?? _model,
                ["prompt"] = prompt,
                ["stream"] = stream
            };
            if (options != null) requestBody["options"] = options;
            if (format != null) requestBody["format"] = format;
            if (suffix != null) requestBody["suffix"] = suffix;

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_baseUrl + "generate", content);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("response", out var resp))
            {
                return resp.GetString() ?? string.Empty;
            }
            return responseString;
        }

        // Generate a chat completion (POST /api/chat)
        public async Task<string> ChatAsync(IEnumerable<(string role, string content)> messages, string? model = null, object? options = null, string? format = null)
        {
            var msgList = messages.Select(m => new Dictionary<string, object?>
            {
                ["role"] = m.role,
                ["content"] = m.content
            }).ToList();
            var requestBody = new Dictionary<string, object?>
            {
                ["model"] = model ?? _model,
                ["messages"] = msgList,
                ["stream"] = false
            };
            if (options != null) requestBody["options"] = options;
            if (format != null) requestBody["format"] = format;

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_baseUrl + "chat", content);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var contentProp))
            {
                return contentProp.GetString() ?? string.Empty;
            }
            return responseString;
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(IEnumerable<(string role, string content)> messages, string? model = null, object? options = null, string? format = null)
        {
            var msgList = messages.Select(m => new Dictionary<string, object?>
            {
                ["role"] = m.role,
                ["content"] = m.content
            }).ToList();
            var requestBody = new Dictionary<string, object?>
            {
                ["model"] = model ?? _model,
                ["messages"] = msgList,
                ["stream"] = true
            };
            if (options != null) requestBody["options"] = options;
            if (format != null) requestBody["format"] = format;

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using (var response = await _httpClient.PostAsync(_baseUrl + "chat", content))
            {                
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    string? line;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        using var doc = JsonDocument.Parse(line);

                        if (doc.RootElement.TryGetProperty("message", out var msgElem))
                        {
                            if (msgElem.TryGetProperty("content", out var msgContent))
                            {
                                yield return msgContent.GetString() ?? string.Empty;
                            }
                            else
                            {
                                yield return string.Empty;
                            }
                        }
                    }
                }
            }
        }

        // List local models (GET /api/tags)
        public async Task<string[]> ListLocalModelsAsync()
        {
            var response = await _httpClient.GetAsync(_baseUrl + "tags");
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("models", out var modelsElem) && modelsElem.ValueKind == JsonValueKind.Array)
            {
                return modelsElem.EnumerateArray().Select(m => m.GetProperty("name").GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray();
            }
            return Array.Empty<string>();
        }

        // Show model information (POST /api/show)
        public async Task<string> ShowModelInfoAsync(string model)
        {
            var requestBody = new { model };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_baseUrl + "show", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        // Pull a model (POST /api/pull)
        public async Task<string> PullModelAsync(string model)
        {
            var requestBody = new { model };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_baseUrl + "pull", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        // Delete a model (DELETE /api/delete)
        public async Task<bool> DeleteModelAsync(string model)
        {
            var requestBody = new { model };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Delete, _baseUrl + "delete") { Content = content };
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        // Generate embeddings (POST /api/embed)
        public async Task<float[][]> GenerateEmbeddingsAsync(string[] input, string? model = null, object? options = null)
        {
            var requestBody = new Dictionary<string, object?>
            {
                ["model"] = model ?? _model,
                ["input"] = input
            };
            if (options != null) requestBody["options"] = options;
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_baseUrl + "embed", content);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("embeddings", out var embElem) && embElem.ValueKind == JsonValueKind.Array)
            {
                var list = new List<float[]>();
                foreach (var arr in embElem.EnumerateArray())
                {
                    var floats = arr.EnumerateArray().Select(x => x.GetSingle()).ToArray();
                    list.Add(floats);
                }
                return list.ToArray();
            }
            return Array.Empty<float[]>();
        }

        // Get Ollama version (GET /api/version)
        public async Task<string> GetVersionAsync()
        {
            var response = await _httpClient.GetAsync(_baseUrl + "version");
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            if (doc.RootElement.TryGetProperty("version", out var v))
            {
                return v.GetString() ?? string.Empty;
            }
            return responseString;
        }
    }
}