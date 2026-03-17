using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using StaticWGen.Core.Models;
using YamlDotNet.RepresentationModel;

namespace StaticWGen.Core.Services;

/// <summary>
/// Core markdown processing service with no build-system dependency.
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

    public static Dictionary<string, string> ExtractMetadata(string content, string? fileName = null)
    {
        var pipeline = CreateMinimalPipeline();
        var document = Markdown.Parse(content, pipeline);
        return ExtractMetadata(document, content);
    }

    public static Dictionary<string, string> ExtractMetadata(MarkdownDocument document, string content)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock == null)
            return metadata;

        var yaml = content.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length);
        YamlStream yamlStream;
        try
        {
            yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(yaml));
        }
        catch
        {
            return metadata;
        }

        if (yamlStream.Documents.Count == 0)
            return metadata;

        if (yamlStream.Documents[0].RootNode is not YamlMappingNode rootNode)
            return metadata;

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

            if (value != null)
                metadata[keyNode.Value] = value;
        }

        return metadata;
    }

    public static string RemoveFrontMatter(MarkdownDocument document, string content)
    {
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock != null)
        {
            var contentStart = yamlBlock.Span.End + 1;
            return content.Substring(contentStart).TrimStart();
        }
        return content;
    }

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

        var document = Markdown.Parse(markdownContent, pipeline);
        htmlRenderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    public static (Dictionary<string, string> Metadata, string Content) ParseMarkdownFile(string fileContent, string? fileName = null)
    {
        var pipeline = CreateMinimalPipeline();
        var document = Markdown.Parse(fileContent, pipeline);
        var metadata = ExtractMetadata(document, fileContent);
        var markdownContent = RemoveFrontMatter(document, fileContent);
        return (metadata, markdownContent);
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
                renderer.Write("<pre class=\"mermaid\">");
                renderer.WriteLeafRawLines(obj, true, true);
                renderer.WriteLine("</pre>");
            }
            else
            {
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
