using StoryGenly.AI;
using System.Text.Json;
using Serilog;

namespace StoryGenly.Engine;

public record StoryGenerationPhase(string Name, string PromptTemplate);

public class StoryEngine
{
    private readonly ModelBridge _modelBridge;
    private readonly VectorDb _vectorDb;
    private readonly string _outputFolder;
    private readonly string _promptsFolder;

    public StoryEngine(ModelBridge modelBridge, VectorDb vectorDb, string outputFolder, string promptsFolder)
    {
        _modelBridge = modelBridge;
        _vectorDb = vectorDb;
        _outputFolder = outputFolder;
        _promptsFolder = promptsFolder;
    }

    public async Task GenerateStoryAsync(bool forceNew = false)
    {
        if(!Directory.Exists(_outputFolder))
        {
            Directory.CreateDirectory(_outputFolder);
        }

        var phases = new List<StoryGenerationPhase>
        {
            new("bible", "bible_prompt.txt"),
            new("chapters", "chapters_prompt.txt"),
            new("scenes", "scenes_prompt.txt")
        };

        if (forceNew)
        {
            Log.Information("⚠️ Force new story generation enabled. Previous outputs will be deleted.");

            foreach (var phase in phases)
            {
                var outputPath = Path.Combine(_outputFolder, $"{phase.Name}.txt");
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    Log.Information("🗑️ Deleted previous output file: {OutputPath}", outputPath);
                }
            }
        }

        StoryGenerationPhase biblePhase = phases.Where(p => p.Name == "bible").First();;
        var bibleContext = await HandlePhaseAsync(biblePhase, Enumerable.Empty<JsonElement>());
        NdJsonParser.WriteToFile(Path.Combine(_outputFolder, $"{biblePhase.Name}.txt"), bibleContext);

        StoryGenerationPhase chaptersPhase = phases.Where(p => p.Name == "chapters").First();
        var chaptersContext = await HandlePhaseAsync(chaptersPhase, bibleContext);
        NdJsonParser.WriteToFile(Path.Combine(_outputFolder, $"{chaptersPhase.Name}.txt"), chaptersContext);

        var sceneContext = new List<List<JsonElement>>();

        foreach (var chapterElement in chaptersContext)
        {
            var chapterTitle = NdJsonParser.GetStringProperty(chapterElement, "t");
            if (string.IsNullOrEmpty(chapterTitle))
            {
                Log.Warning("⚠️ Chapter element is missing 't' property. Skipping.");
                continue;
            }

            int chapterIndex = NdJsonParser.GetIntProperty(chapterElement, "i") ?? 0;
            Log.Information("📖 Generating scenes for chapter: {ChapterIndex} {ChapterTitle}", chapterIndex, chapterTitle);

            StoryGenerationPhase scenesPhase = phases.Where(p => p.Name == "scenes").First();
            var currentChapterContext = new List<JsonElement>(bibleContext) { chapterElement };
            var chapterScenesContext = await HandlePhaseAsync(scenesPhase, currentChapterContext);
            NdJsonParser.WriteToFile(Path.Combine(_outputFolder, $"{chapterIndex}_scenes.txt"), chapterScenesContext);
        }
    }

    public async Task<IEnumerable<JsonElement>> HandlePhaseAsync(StoryGenerationPhase phase, IEnumerable<JsonElement> context)
    {
        Log.Information("📚 Starting story generation phase: {PhaseName}", phase.Name);

        var previousOutput = NdJsonParser.ToNdJsonString(context);

        var prompt = await File.ReadAllTextAsync(Path.Combine(_promptsFolder, phase.PromptTemplate));
        Log.Information("📝 Loaded prompt template: {PromptTemplate}", phase.PromptTemplate);

        prompt = prompt.Replace("{{previous_output}}", previousOutput);
        Log.Debug("Prompt content: {Prompt}", prompt);

        Log.Information("🤖 AI is thinking and generating your story...");
        var response = await _modelBridge.GenerateAsync(prompt);
        Log.Information("✨ Story content generated");
        Log.Debug("Generated content: {Response}", response);

        response = PostProcess(response);

        Log.Information("🎉 Phase '{PhaseName}' completed successfully!", phase.Name);

        var jsonElements = NdJsonParser.Parse(response);
        return jsonElements;
    }
    
    private string PostProcess(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var validLines = new List<string>();
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;
                
            try
            {
                JsonDocument.Parse(trimmedLine);
                validLines.Add(trimmedLine);
            }
            catch (JsonException)
            {
                Log.Warning("⚠️ Skipping invalid JSON line: {Line}", trimmedLine);
            }
        }
        
        return string.Join('\n', validLines);
    }
}