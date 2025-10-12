using System.Text.Json;
using Microsoft.VisualBasic;
using StoryGenly.Models;

namespace StoryGenly.Gutenberg
{
    public class GutenbergDownloader
    {
        private readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        });

        private readonly string _downloadPath;
        private readonly string _gutendexBaseUrl;

        public GutenbergDownloader(string gutendexBaseUrl, string downloadPath)
        {
            _gutendexBaseUrl = gutendexBaseUrl;
            _downloadPath = downloadPath;

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("StoryGenly/1.0 (+contact: me@example.com)");
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task DownloadAllBookResultsAsync(string query)
        {
            Console.WriteLine($"Current working directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"Download path: {_downloadPath}");

            if (!Directory.Exists(_downloadPath))
            {
                Directory.CreateDirectory(_downloadPath);
            }

            var bookResults = await GetBookResultsAsync(query);
            if (bookResults == null)
            {
                throw new InvalidOperationException("Failed to retrieve book results.");
            }

            do
            {
                Console.WriteLine("Downloading book results page.");
                await DownloadBookResultsPageAsync(bookResults);
                
                if (!string.IsNullOrEmpty(bookResults.Next))
                {
                    bookResults = await GetBookResultsFromUrlAsync(bookResults.Next);
                }
                else
                {
                    bookResults = null;
                }
            } while (bookResults != null);
        }

        public async Task DownloadBookResultsPageAsync(GutenbergResponse bookResultsPage)
        {
            foreach (var bookResult in bookResultsPage.Results)
            {
                Console.WriteLine($"Downloading book: {bookResult.Title}");
                var bookFilePath = Path.Combine(_downloadPath, $"{bookResult.Id}.txt");

                if (File.Exists(bookFilePath))
                {
                    Console.WriteLine($"Book already downloaded: {bookResult.Title}");
                    continue;
                }

                var url = bookResult.Formats.TextPlainUtf8;
                if (string.IsNullOrEmpty(url))
                {
                    url = bookResult.Formats.TextPlainAscii;
                }

                if (!string.IsNullOrEmpty(url))
                {
                    Console.WriteLine($"Attempting to download from: {url}");
                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        Console.WriteLine($"Response status: {response.StatusCode}");

                        if (response.StatusCode == System.Net.HttpStatusCode.Found ||
                            response.StatusCode == System.Net.HttpStatusCode.Redirect)
                        {
                            // Handle manual redirect if automatic redirect failed
                            var location = response.Headers.Location?.ToString();
                            if (!string.IsNullOrEmpty(location))
                            {
                                Console.WriteLine($"Following redirect to: {location}");
                                response = await _httpClient.GetAsync(location);
                            }
                        }

                        response.EnsureSuccessStatusCode();

                        var bookContent = await response.Content.ReadAsStringAsync();
                        await File.WriteAllTextAsync(bookFilePath, bookContent);
                        Console.WriteLine($"Successfully downloaded: {bookResult.Title}");
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.WriteLine($"Failed to download {bookResult.Title}: {ex.Message}");
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine($"No suitable text format found for book: {bookResult.Title}");
                }
            }
        }

        public async Task<GutenbergResponse?> GetBookResultsAsync(string? query = null)
        {
            var url = $"{_gutendexBaseUrl}{query}";
            return await GetBookResultsFromUrlAsync(url);
        }

        public async Task<GutenbergResponse?> GetBookResultsFromUrlAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GutenbergResponse>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}