using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using StaticWGen.Core.Models;
using YamlDotNet.RepresentationModel;

namespace StaticWGen.Core.Services;

/// <summary>
/// The Markdown pipeline the site build, the dev server and the tests all share.
/// Warnings (malformed front-matter, suspicious values) go to the optional <c>onWarning</c> callback.
/// </summary>
public static class MarkdownService
{
    public static MarkdownPipeline CreatePipeline()
    {
        return new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .UseMathematics()
            .UseEmojiAndSmiley()
            .UseSmartyPants()
            .UseAdvancedExtensions()
            .Build();
    }

    public static MarkdownPipeline CreateMinimalPipeline()
    {
        return new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .Build();
    }

    public static Dictionary<string, string> ExtractMetadata(string content, string? fileName = null,
        Action<string>? onWarning = null)
    {
        var document = Markdown.Parse(content, CreateMinimalPipeline());
        return ExtractMetadata(document, content, fileName, onWarning);
    }

    public static Dictionary<string, string> ExtractMetadata(MarkdownDocument document, string content,
        string? fileName = null, Action<string>? onWarning = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var context = fileName != null ? $" in {fileName}" : "";
        void Warn(string message) => onWarning?.Invoke(message);

        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock == null)
            return metadata;

        var yaml = content.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length);
        var yamlStream = new YamlStream();
        try
        {
            yamlStream.Load(new StringReader(yaml));
        }
        catch (Exception ex)
        {
            Warn($"Malformed YAML front-matter{context}: {ex.Message}");
            return metadata;
        }

        if (yamlStream.Documents.Count == 0)
        {
            Warn($"Empty YAML front-matter{context}");
            return metadata;
        }

        if (yamlStream.Documents[0].RootNode is not YamlMappingNode rootNode)
        {
            Warn($"YAML front-matter root is not a mapping{context}");
            return metadata;
        }

        foreach (var entry in rootNode.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode || string.IsNullOrEmpty(keyNode.Value))
                continue;

            var value = entry.Value switch
            {
                YamlScalarNode scalar => scalar.Value ?? "",
                YamlSequenceNode seq => string.Join(", ",
                    seq.Children.OfType<YamlScalarNode>().Select(n => n.Value ?? "")),
                _ => null
            };

            if (value == null)
            {
                Warn($"Unsupported YAML value type for key '{keyNode.Value}'{context} (expected scalar or sequence)");
                continue;
            }

            metadata[keyNode.Value] = value;
        }

        if (metadata.TryGetValue("date", out var dateStr) && !string.IsNullOrEmpty(dateStr) &&
            !DateTime.TryParse(dateStr, out _))
            Warn($"Invalid date format '{dateStr}'{context} — expected a parseable date (e.g., 2024-09-13)");

        if (metadata.TryGetValue("image", out var imageStr) && !string.IsNullOrEmpty(imageStr) &&
            !imageStr.StartsWith("./") && !imageStr.StartsWith("/") &&
            !imageStr.StartsWith("http://") && !imageStr.StartsWith("https://") &&
            !imageStr.Contains('.'))
            Warn($"Image value '{imageStr}'{context} does not look like a valid path or URL");

        return metadata;
    }

    public static string RemoveFrontMatter(string content) =>
        RemoveFrontMatter(Markdown.Parse(content, CreateMinimalPipeline()), content);

    public static string RemoveFrontMatter(MarkdownDocument document, string content)
    {
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        return yamlBlock != null ? content.Substring(yamlBlock.Span.End + 1).TrimStart() : content;
    }

    /// <summary>Converts Markdown (without front-matter) to HTML, rendering Mermaid and Prism code blocks.</summary>
    public static string ConvertToHtml(string markdownContent)
    {
        var pipeline = CreatePipeline();
        var writer = new StringWriter();
        var htmlRenderer = new HtmlRenderer(writer);
        pipeline.Setup(htmlRenderer);

        var defaultRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();
        if (defaultRenderer != null)
            htmlRenderer.ObjectRenderers.Remove(defaultRenderer);
        htmlRenderer.ObjectRenderers.AddIfNotAlready(new MermaidAwareCodeBlockRenderer());

        htmlRenderer.Render(Markdown.Parse(markdownContent, pipeline));
        writer.Flush();
        return writer.ToString();
    }

    /// <summary>Parses a full page (front-matter + body) into its metadata and rendered HTML body.</summary>
    public static (Dictionary<string, string> Metadata, string Html) RenderPage(string content,
        string? fileName = null, Action<string>? onWarning = null)
    {
        var document = Markdown.Parse(content, CreatePipeline());
        var metadata = ExtractMetadata(document, content, fileName, onWarning);
        return (metadata, ConvertToHtml(RemoveFrontMatter(document, content)));
    }

    /// <summary>Reads a Markdown file and returns its metadata and its body without front-matter.</summary>
    public static (Dictionary<string, string> Metadata, string Content) ParseMarkdownFile(string path,
        Action<string>? onWarning = null)
    {
        var content = File.ReadAllText(path);
        var document = Markdown.Parse(content, CreateMinimalPipeline());
        var metadata = ExtractMetadata(document, content, Path.GetFileName(path), onWarning);
        return (metadata, RemoveFrontMatter(document, content));
    }

    public static ContentStatus GetContentStatus(Dictionary<string, string> metadata, bool includeDrafts = false)
    {
        if (metadata.TryGetValue("draft", out var draft) &&
            string.Equals(draft, "true", StringComparison.OrdinalIgnoreCase))
            return includeDrafts ? ContentStatus.Draft : ContentStatus.Excluded;

        if (metadata.TryGetValue("publishDate", out var publishDateStr) &&
            DateTime.TryParse(publishDateStr, out var publishDate) &&
            publishDate.Date > DateTime.Today)
            return includeDrafts ? ContentStatus.Scheduled : ContentStatus.Excluded;

        if (metadata.TryGetValue("status", out var status) &&
            string.Equals(status, "archived", StringComparison.OrdinalIgnoreCase))
            return ContentStatus.Archived;

        return ContentStatus.Published;
    }
}

public class MermaidAwareCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
{
    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        if (obj is FencedCodeBlock fencedCodeBlock)
        {
            var language = fencedCodeBlock.Info ?? "plaintext";
            if (string.Equals(language, "mermaid", StringComparison.OrdinalIgnoreCase))
            {
                // No <code> wrapper, so Mermaid.js can process the block
                renderer.Write("<pre class=\"mermaid\">");
                renderer.WriteLeafRawLines(obj, true, true);
                renderer.WriteLine("</pre>");
            }
            else
            {
                // Prism-compatible language class
                renderer.Write("<pre><code class=\"language-").Write(language).Write("\">");
                renderer.WriteLeafRawLines(obj, true, true);
                renderer.WriteLine("</code></pre>");
            }
        }
        else
        {
            renderer.Write("<pre><code>");
            renderer.WriteLeafRawLines(obj, true, true);
            renderer.WriteLine("</code></pre>");
        }
    }
}
