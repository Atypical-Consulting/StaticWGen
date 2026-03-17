namespace StaticWGen.Core.Models;

/// <summary>
/// Represents metadata extracted from YAML front-matter.
/// </summary>
public record PageMetadata
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Author { get; init; } = "";
    public string Date { get; init; } = "";
    public string Keywords { get; init; } = "";
    public string Image { get; init; } = "";
    public string Lang { get; init; } = "en";
    public bool Draft { get; init; }
    public bool NoIndex { get; init; }
    public Dictionary<string, string> Raw { get; init; } = new();

    public static PageMetadata FromDictionary(Dictionary<string, string> metadata)
    {
        return new PageMetadata
        {
            Title = metadata.GetValueOrDefault("title", ""),
            Description = metadata.GetValueOrDefault("description", ""),
            Author = metadata.GetValueOrDefault("author", ""),
            Date = metadata.GetValueOrDefault("date", ""),
            Keywords = metadata.GetValueOrDefault("keywords", ""),
            Image = metadata.GetValueOrDefault("image", ""),
            Lang = metadata.GetValueOrDefault("lang", "en"),
            Draft = metadata.TryGetValue("draft", out var d) &&
                    string.Equals(d, "true", StringComparison.OrdinalIgnoreCase),
            NoIndex = metadata.TryGetValue("noindex", out var n) &&
                      string.Equals(n, "true", StringComparison.OrdinalIgnoreCase),
            Raw = metadata
        };
    }
}
