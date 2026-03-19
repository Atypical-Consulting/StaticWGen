using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Transforms {{alert:type}}...{{/alert}} shortcodes into styled alert boxes.
/// Supported types: info, warning, danger, success.
/// Usage in markdown:
///   {{alert:warning}}Be careful with this operation!{{/alert}}
/// </summary>
public class AlertBoxTransformer : IContentTransformer
{
    public int Order => 20;
    public string Name => "Alert Box";

    public string Transform(string html, Dictionary<string, string> metadata)
    {
        // Match alert shortcodes, including any wrapping <p> tags from Markdown rendering
        return Regex.Replace(html, @"(?:<p>)?\{\{alert:(\w+)\}\}(.*?)\{\{/alert\}\}(?:</p>)?", match =>
        {
            var type = match.Groups[1].Value.ToLowerInvariant();
            var content = match.Groups[2].Value.Trim();

            var (icon, borderColor, bgColor) = type switch
            {
                "warning" => ("\u26a0\ufe0f", "#f0ad4e", "rgba(240,173,78,0.1)"),
                "danger" => ("\u274c", "#d9534f", "rgba(217,83,79,0.1)"),
                "success" => ("\u2705", "#5cb85c", "rgba(92,184,92,0.1)"),
                _ => ("\u2139\ufe0f", "#5bc0de", "rgba(91,192,222,0.1)") // info
            };

            return $"""
                <div role="alert" style="padding:1rem;margin:1rem 0;border-left:4px solid {borderColor};background:{bgColor};border-radius:0 4px 4px 0;">
                  <strong>{icon} {char.ToUpper(type[0]) + type.Substring(1)}</strong><br>{content}
                </div>
                """;
        }, RegexOptions.Singleline);
    }
}
