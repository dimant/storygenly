using StoryGenly.AI;
using System.Text.Json;

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

    public async Task GenerateStoryAsync()
    {
        if(!Directory.Exists(_outputFolder))
        {
            Directory.CreateDirectory(_outputFolder);
        }

        var phases = new List<StoryGenerationPhase>
        {
            new("bible", "bible_prompt.txt"),
            new("chapters", "chapters_prompt.txt")
        };

        StoryGenerationPhase? previousPhase = null;
        foreach (var phase in phases)
        {
            if (!File.Exists(Path.Combine(_outputFolder, $"{phase.Name}.txt")))
            {
                await HandlePhaseAsync(phase, previousPhase);
            }
            previousPhase = phase;
        }
    }

    public async Task HandlePhaseAsync(StoryGenerationPhase phase, StoryGenerationPhase? previousPhase = null)
    {
        Console.WriteLine($"📚 Starting story generation phase: {phase.Name}");

        if (previousPhase != null)
        {
            Console.WriteLine($"🔗 Previous phase: {previousPhase.Name}");
        }

        var previousOutput = previousPhase != null
            ? await File.ReadAllTextAsync(Path.Combine(_outputFolder, $"{previousPhase.Name}.txt"))
            : string.Empty;

        var prompt = await File.ReadAllTextAsync(Path.Combine(_promptsFolder, phase.PromptTemplate));
        Console.WriteLine($"📝 Loaded prompt template: {phase.PromptTemplate}");

        prompt = prompt.Replace("{{previous_output}}", previousOutput);
        Console.WriteLine(prompt);

        Console.WriteLine("🤖 AI is thinking and generating your story...");
        var response = await _modelBridge.GenerateAsync(prompt);
        Console.WriteLine("✨ Story content:");
        Console.WriteLine();
        Console.WriteLine(response);
        Console.WriteLine();
        
        var outputPath = Path.Combine(_outputFolder, $"{phase.Name}.txt");
        await File.WriteAllTextAsync(outputPath, response);
        
        Console.WriteLine($"💾 Story saved to: {outputPath}");
        Console.WriteLine($"🎉 Phase '{phase.Name}' completed successfully!");
        Console.WriteLine();
    }
}