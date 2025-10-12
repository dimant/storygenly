using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StoryGenly.AI
{
    public class ModelBridge
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "http://localhost:11434/api/";
        private string _model = "dolphin3:latest";

        /// <summary>
        /// Initializes a new instance of the ModelBridge class to interact with an Ollama API server.
        /// </summary>
        /// <param name="baseUrl">The base URL of the Ollama API server (e.g., "http://localhost:11434/api/")</param>
        /// <param name="model">Optional default model name to use for requests. If not specified, uses "dolphin3:latest"</param>
        public ModelBridge(string baseUrl, string? model = null)
        {
            _httpClient = new HttpClient();
            _baseUrl = baseUrl;
            if (!string.IsNullOrWhiteSpace(model))
                _model = model;
        }

        /// <summary>
        /// Generates a text completion based on a given prompt using the Ollama API.
        /// Uses the /api/generate endpoint to produce a single response to the provided prompt.
        /// </summary>
        /// <param name="prompt">The input text prompt to generate a completion for</param>
        /// <param name="model">Optional model name to use. If null, uses the default model specified in constructor</param>
        /// <param name="options">Optional model-specific parameters (temperature, top_p, etc.)</param>
        /// <param name="format">Optional response format specification (e.g., "json")</param>
        /// <param name="suffix">Optional text to append after the generated content</param>
        /// <param name="stream">Whether to stream the response. Currently set to false for this method</param>
        /// <returns>The generated text completion as a string</returns>
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

        /// <summary>
        /// Generates a chat completion based on a conversation history using the Ollama API.
        /// Uses the /api/chat endpoint to continue a conversation with context from previous messages.
        /// </summary>
        /// <param name="messages">Collection of messages in the conversation. Each message contains a role ("user", "assistant", "system") and content</param>
        /// <param name="model">Optional model name to use. If null, uses the default model specified in constructor</param>
        /// <param name="options">Optional model-specific parameters (temperature, top_p, etc.)</param>
        /// <param name="format">Optional response format specification (e.g., "json")</param>
        /// <returns>The assistant's response as a string</returns>
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

        /// <summary>
        /// Generates a streaming chat completion based on a conversation history using the Ollama API.
        /// Uses the /api/chat endpoint with streaming enabled to receive the response incrementally as it's generated.
        /// This is useful for real-time applications where you want to display the response as it's being generated.
        /// </summary>
        /// <param name="messages">Collection of messages in the conversation. Each message contains a role ("user", "assistant", "system") and content</param>
        /// <param name="model">Optional model name to use. If null, uses the default model specified in constructor</param>
        /// <param name="options">Optional model-specific parameters (temperature, top_p, etc.)</param>
        /// <param name="format">Optional response format specification (e.g., "json")</param>
        /// <returns>An async enumerable of string chunks representing the streaming response</returns>
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

        /// <summary>
        /// Retrieves a list of all locally available models from the Ollama server.
        /// Uses the /api/tags endpoint to get information about models that have been downloaded and are ready to use.
        /// </summary>
        /// <returns>An array of model names that are available locally</returns>
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

        /// <summary>
        /// Retrieves detailed information about a specific model from the Ollama server.
        /// Uses the /api/show endpoint to get metadata, parameters, and other details about the specified model.
        /// </summary>
        /// <param name="model">The name of the model to get information about</param>
        /// <returns>A JSON string containing detailed model information including parameters, template, and metadata</returns>
        public async Task<string> ShowModelInfoAsync(string model)
        {
            var requestBody = new { model };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_baseUrl + "show", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Downloads a model from the Ollama registry to the local server.
        /// Uses the /api/pull endpoint to download and install a model, making it available for use.
        /// This operation may take some time depending on the model size and network speed.
        /// </summary>
        /// <param name="model">The name of the model to download (e.g., "llama2", "codellama:7b")</param>
        /// <returns>A string containing the response from the pull operation, typically including download progress information</returns>
        public async Task<string> PullModelAsync(string model)
        {
            var requestBody = new { model };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_baseUrl + "pull", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Deletes a locally stored model from the Ollama server to free up disk space.
        /// Uses the /api/delete endpoint to remove a model that was previously downloaded.
        /// </summary>
        /// <param name="model">The name of the model to delete</param>
        /// <returns>True if the model was successfully deleted, false otherwise</returns>
        public async Task<bool> DeleteModelAsync(string model)
        {
            var requestBody = new { model };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Delete, _baseUrl + "delete") { Content = content };
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Generates vector embeddings for the provided text inputs using the Ollama API.
        /// Uses the /api/embed endpoint to convert text into numerical vector representations that can be used
        /// for semantic similarity comparisons, clustering, or other machine learning tasks.
        /// </summary>
        /// <param name="input">Array of text strings to generate embeddings for</param>
        /// <param name="model">Optional model name to use for embedding generation. If null, uses the default model</param>
        /// <param name="options">Optional model-specific parameters for embedding generation</param>
        /// <returns>A 2D array where each inner array represents the embedding vector for the corresponding input text</returns>
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

        /// <summary>
        /// Retrieves the version information of the running Ollama server.
        /// Uses the /api/version endpoint to get details about the server version, which can be useful
        /// for compatibility checking and debugging purposes.
        /// </summary>
        /// <returns>The version string of the Ollama server</returns>
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