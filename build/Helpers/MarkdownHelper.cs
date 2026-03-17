using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Nuke.Common.IO;
using YamlDotNet.RepresentationModel;

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

    public static Dictionary<string, string> ExtractMetadata(string content)
    {
        var pipeline = CreateMinimalPipeline();
        var document = Markdown.Parse(content, pipeline);
        return ExtractMetadata(document, content);
    }

    public static Dictionary<string, string> ExtractMetadata(MarkdownDocument document, string content)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock != null)
        {
            var yaml = content.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length);
            var input = new StringReader(yaml);
            var yamlStream = new YamlStream();
            yamlStream.Load(input);

            var rootNode = (YamlMappingNode)yamlStream.Documents[0].RootNode;
            foreach (var entry in rootNode.Children)
            {
                var key = ((YamlScalarNode)entry.Key).Value;
                var value = ((YamlScalarNode)entry.Value).Value;
                metadata[key] = value;
            }
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

    public static (Dictionary<string, string> Metadata, string Content) ParseMarkdownFile(AbsolutePath file)
    {
        var content = file.ReadAllText();
        var pipeline = CreateMinimalPipeline();
        var document = Markdown.Parse(content, pipeline);
        var metadata = ExtractMetadata(document, content);
        var markdownContent = RemoveFrontMatter(document, content);
        return (metadata, markdownContent);
    }
}
