using Microsoft.Extensions.Configuration;

using StoryGenly.AI;
using StoryGenly.Gutenberg;

namespace StoryGenly
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            var modelBridge = new ModelBridge(
                config["ModelBridge:BaseUrl"] ?? throw new InvalidOperationException("BaseUrl is not configured"),
                config["ModelBridge:DefaultModel"]);

            var gutenbergDownloader =
                new GutenbergDownloader(
                    config["Gutenberg:BaseUrl"] ?? throw new InvalidOperationException("Gutenberg BaseUrl is not configured"),
                    config["Gutenberg:DownloadPath"] ?? throw new InvalidOperationException("Gutenberg DownloadPath is not configured"));

            await gutenbergDownloader.DownloadBookResultsAsync("?topic=science+fiction");
        }
    }
}