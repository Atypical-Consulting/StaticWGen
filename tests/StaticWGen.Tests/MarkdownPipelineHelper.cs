using System.IO;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using YamlDotNet.RepresentationModel;

namespace StaticWGen.Tests;

/// <summary>
/// Helper that replicates the core markdown processing logic from IGenerateWebsite
/// for testability.
/// </summary>
public static class MarkdownPipelineHelper
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

    public static Dictionary<string, string> ExtractMetadata(string content)
    {
        var pipeline = CreatePipeline();
        var document = Markdown.Parse(content, pipeline);
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

    public static string RemoveFrontMatter(string content)
    {
        var pipeline = CreatePipeline();
        var document = Markdown.Parse(content, pipeline);

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
        return Markdown.ToHtml(markdownContent, pipeline);
    }

    public static string ConvertToHtmlWithMermaidRenderer(string markdownContent)
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
