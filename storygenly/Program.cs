using System;
using Microsoft.Extensions.Configuration;

using StoryGenly.AI;

namespace StoryGenly
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            var modelBridge = new ModelBridge(
                config["ModelBridge:BaseUrl"] ?? throw new InvalidOperationException("BaseUrl is not configured"),
                config["ModelBridge:DefaultModel"]);
        }
    }
}