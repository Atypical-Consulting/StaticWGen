using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Nuke.Common.IO;
using YamlDotNet.RepresentationModel;
using static Serilog.Log;

public static class MarkdownHelper
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
        return ExtractMetadata(document, content, fileName);
    }

    public static Dictionary<string, string> ExtractMetadata(MarkdownDocument document, string content, string? fileName = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var context = fileName != null ? $" in {fileName}" : "";

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
        catch (Exception ex)
        {
            Warning("Malformed YAML front-matter{Context}: {Message}", context, ex.Message);
            return metadata;
        }

        if (yamlStream.Documents.Count == 0)
        {
            Warning("Empty YAML front-matter{Context}", context);
            return metadata;
        }

        if (yamlStream.Documents[0].RootNode is not YamlMappingNode rootNode)
        {
            Warning("YAML front-matter root is not a mapping{Context}", context);
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
                Warning("Unsupported YAML value type for key '{Key}'{Context} (expected scalar or sequence)",
                    keyNode.Value, context);
                continue;
            }

            metadata[keyNode.Value] = value;
        }

        // Validate known fields
        ValidateMetadata(metadata, context);

        return metadata;
    }

    private static void ValidateMetadata(Dictionary<string, string> metadata, string context)
    {
        // Validate date field is parseable
        if (metadata.TryGetValue("date", out var dateStr) && !string.IsNullOrEmpty(dateStr))
        {
            if (!DateTime.TryParse(dateStr, out _))
                Warning("Invalid date format '{Date}'{Context} — expected a parseable date (e.g., 2024-09-13)",
                    dateStr, context);
        }

        // Validate image field looks like a path or URL
        if (metadata.TryGetValue("image", out var imageStr) && !string.IsNullOrEmpty(imageStr))
        {
            if (!imageStr.StartsWith("./") && !imageStr.StartsWith("/") &&
                !imageStr.StartsWith("http://") && !imageStr.StartsWith("https://") &&
                !imageStr.Contains('.'))
                Warning("Image value '{Image}'{Context} does not look like a valid path or URL",
                    imageStr, context);
        }
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

    public static (Dictionary<string, string> Metadata, string Content) ParseMarkdownFile(AbsolutePath file)
    {
        var content = file.ReadAllText();
        var pipeline = CreateMinimalPipeline();
        var document = Markdown.Parse(content, pipeline);
        var metadata = ExtractMetadata(document, content, file.Name);
        var markdownContent = RemoveFrontMatter(document, content);
        return (metadata, markdownContent);
    }
}
