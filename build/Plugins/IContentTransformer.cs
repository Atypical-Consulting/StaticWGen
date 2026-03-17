using System.Collections.Generic;

/// <summary>
/// Interface for content transformers that process HTML after Markdown conversion.
/// Transformers are applied in Order sequence (lower = earlier).
/// </summary>
public interface IContentTransformer
{
    /// <summary>Execution order (lower runs first).</summary>
    int Order { get; }

    /// <summary>Human-readable name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Transform the HTML content. Return the modified HTML.
    /// </summary>
    string Transform(string html, Dictionary<string, string> metadata);
}
