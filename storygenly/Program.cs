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
                await HandleGutenbergDownload(config);
                return;
            }

            if (parsedArgs.ContainsKey("extract-chunks"))
            {
                await HandleExtractChunksAsync(config);
                return;
            }

            // Default behavior if no arguments
            var modelBridge = new ModelBridge(
                config["ModelBridge:BaseUrl"] ?? throw new InvalidOperationException("BaseUrl is not configured"),
                config["ModelBridge:DefaultModel"]);
            var vectorDb = new AI.VectorDb(
                config["VectorDb:dbFilePath"] ?? throw new InvalidOperationException("VectorDb dbFilePath is not configured"));

            var storyGenerator = new Engine.StoryEngine(modelBridge, vectorDb,
                config["StoryEngine:OutputFolder"] ?? throw new InvalidOperationException("StoryEngine OutputFolder is not configured"),
                config["StoryEngine:PromptsFolder"] ?? throw new InvalidOperationException("StoryEngine PromptsFolder is not configured"));

            await storyGenerator.GenerateStoryAsync();
        }

        private static async Task HandleExtractChunksAsync(IConfigurationRoot config)
        {
            var vectorDb = new AI.VectorDb(config["VectorDb:dbFilePath"] ?? throw new InvalidOperationException("VectorDb dbFilePath is not configured"));
            var modelBridge = new ModelBridge(
                config["ModelBridge:BaseUrl"] ?? throw new InvalidOperationException("BaseUrl is not configured"),
                config["ModelBridge:DefaultModel"]);

            var chunks = TextChunker.ChunkDirectory(
                config["Gutenberg:DownloadPath"] ?? throw new InvalidOperationException("Gutenberg DownloadPath is not configured"),
                maxChars: 1600,
                overlapChars: 200);
            foreach (var chunk in chunks)
            {
                Console.WriteLine($"Embedding chunk from {chunk.filename} [{chunk.index}].");
                var id = Path.GetFileNameWithoutExtension(chunk.filename) + $"_{chunk.index}";
                try
                {
                    var embedding = await modelBridge.GenerateEmbeddingsAsync(new[] { chunk.code_chunk }, "jina/jina-embeddings-v2-base-en:latest");
                    vectorDb.InsertRow(id, chunk.filename, chunk.index, chunk.code_chunk, chunk.hash, embedding[0]);
                }
                catch
                {
                    Console.WriteLine($"Error embedding chunk {id}: {chunk.code_chunk}");
                }
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

        private static async Task HandleGutenbergDownload(IConfiguration config)
        {
            var query = config["Gutenberg:DownloadQuery"] ?? throw new InvalidOperationException("Gutenberg DownloadQuery is not configured");
            Console.WriteLine($"Downloading books with query: {query}");

            var gutenbergDownloader =
                new GutenbergDownloader(
                    config["Gutenberg:BaseUrl"] ?? throw new InvalidOperationException("Gutenberg BaseUrl is not configured"),
                    config["Gutenberg:DownloadPath"] ?? throw new InvalidOperationException("Gutenberg DownloadPath is not configured"));

            await gutenbergDownloader.DownloadAllBookResultsAsync(query);
        }
    }
}