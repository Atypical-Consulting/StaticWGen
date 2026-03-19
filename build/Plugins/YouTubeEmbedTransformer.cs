using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Transforms {{youtube:VIDEO_ID}} shortcodes into responsive YouTube embeds.
/// Usage in markdown: {{youtube:dQw4w9WgXcQ}}
/// </summary>
public class YouTubeEmbedTransformer : IContentTransformer
{
    public int Order => 10;
    public string Name => "YouTube Embed";

    public string Transform(string html, Dictionary<string, string> metadata)
    {
        // Match youtube shortcodes, including any wrapping <p> tags from Markdown rendering
        return Regex.Replace(html, @"(?:<p>)?\{\{youtube:([a-zA-Z0-9_-]+)\}\}(?:</p>)?", match =>
        {
            var videoId = match.Groups[1].Value;
            return $"""
                <div style="position:relative;padding-bottom:56.25%;height:0;overflow:hidden;margin:1rem 0;">
                  <iframe src="https://www.youtube-nocookie.com/embed/{videoId}"
                    style="position:absolute;top:0;left:0;width:100%;height:100%;border:0;"
                    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                    allowfullscreen loading="lazy" title="YouTube video"></iframe>
                </div>
                """;
        });
    }
}
