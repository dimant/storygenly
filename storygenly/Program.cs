using Microsoft.Extensions.Configuration;

using StoryGenly.AI;
using StoryGenly.Gutenberg;

namespace StoryGenly
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Parse command line arguments
            var parsedArgs = ParseArguments(args);

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            if (parsedArgs.ContainsKey("download-from-gutenberg"))
            {
                await HandleGutenbergDownload(parsedArgs["download-from-gutenberg"], config);
                return;
            }

            if (parsedArgs.ContainsKey("extract-chunks"))
            {
                HandleExtractChunks(config);
                return;
            }

            // Default behavior if no arguments
            var modelBridge = new ModelBridge(
                config["ModelBridge:BaseUrl"] ?? throw new InvalidOperationException("BaseUrl is not configured"),
                config["ModelBridge:DefaultModel"]);
        }

        private static void HandleExtractChunks(IConfigurationRoot config)
        {
            var chunks = TextChunker.ChunkDirectory(config["Gutenberg:DownloadPath"] ?? throw new InvalidOperationException("Gutenberg DownloadPath is not configured"));
            foreach (var chunk in chunks)
            {
                Console.WriteLine($"Chunk from {chunk.filename} [{chunk.index}]:");
                Console.WriteLine(chunk.code_chunk);
                Console.WriteLine();
            }
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            var result = new Dictionary<string, string>();
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    var key = args[i].Substring(2);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        result[key] = args[i + 1];
                        i++; // Skip next argument as it's the value
                    }
                    else
                    {
                        result[key] = "true"; // Flag without value
                    }
                }
            }
            
            return result;
        }

        private static async Task HandleGutenbergDownload(string query, IConfiguration config)
        {
            Console.WriteLine($"Downloading books with query: {query}");

            var gutenbergDownloader =
                new GutenbergDownloader(
                    config["Gutenberg:BaseUrl"] ?? throw new InvalidOperationException("Gutenberg BaseUrl is not configured"),
                    config["Gutenberg:DownloadPath"] ?? throw new InvalidOperationException("Gutenberg DownloadPath is not configured"));

            await gutenbergDownloader.DownloadAllBookResultsAsync(query);
        }
    }
}