using System.Text.Json;

namespace StoryGenly.Engine;

public record StoryElement
{
    public string Type { get; init; } = string.Empty;
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Summary { get; init; }
    public string? Bio { get; init; }
    public string? Goal { get; init; }
    public string? Flaw { get; init; }
    public string? Description { get; init; }
    public string? Rule { get; init; }
    public string? Evidence { get; init; }
    public string? Owner { get; init; }
    public string? Status { get; init; }
    public string? Purpose { get; init; }
    public string? Location { get; init; }
    public string[]? Tags { get; init; }
    public string[]? Traits { get; init; }
    public Dictionary<string, string>? Attributes { get; init; }

    public static StoryElement FromJson(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new StoryElement
        {
            Type = root.GetProperty("type").GetString() ?? string.Empty,
            Id = root.TryGetProperty("id", out var id) ? id.GetString() : null,
            Name = root.TryGetProperty("n", out var name) ? name.GetString() : null,
            Summary = root.TryGetProperty("sum", out var sum) ? sum.GetString() : null,
            Bio = root.TryGetProperty("bio", out var bio) ? bio.GetString() : null,
            Goal = root.TryGetProperty("goal", out var goal) ? goal.GetString() : null,
            Flaw = root.TryGetProperty("flaw", out var flaw) ? flaw.GetString() : null,
            Description = root.TryGetProperty("desc", out var desc) ? desc.GetString() : null,
            Rule = root.TryGetProperty("rule", out var rule) ? rule.GetString() : null,
            Evidence = root.TryGetProperty("evidence", out var evidence) ? evidence.GetString() : null,
            Owner = root.TryGetProperty("owner", out var owner) ? owner.GetString() : null,
            Status = root.TryGetProperty("status", out var status) ? status.GetString() : null,
            Purpose = root.TryGetProperty("purpose", out var purpose) ? purpose.GetString() : null,
            Location = root.TryGetProperty("loc", out var loc) ? loc.GetString() : null,
            Tags = root.TryGetProperty("tags", out var tags) ?
                tags.EnumerateArray().Select(t => t.GetString() ?? string.Empty).ToArray() : null,
            Traits = root.TryGetProperty("traits", out var traits) ?
                traits.EnumerateArray().Select(t => t.GetString() ?? string.Empty).ToArray() : null,
            Attributes = root.TryGetProperty("attrs", out var attrs) ?
                attrs.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty) : null
        };
    }
}