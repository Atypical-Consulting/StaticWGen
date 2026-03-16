using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Nuke.Common;
using Nuke.Common.IO;
using Scriban;
using YamlDotNet.RepresentationModel;
using static Serilog.Log;

public interface IGenerateTagPages : IHasWebsitePaths
{
    [Parameter("Title of the site")][Required]
    string SiteTitle => TryGetValue(() => SiteTitle);

    Target GenerateTagPages => _ => _
        .DependsOn<IGenerateWebsite>(x => x.GenerateHtml)
        .TriggeredBy<IGenerateWebsite>(x => x.GenerateHtml)
        .Executes(() =>
        {
            Information("Generating tag pages...");

            var tagMap = CollectTags();

            if (tagMap.Count == 0)
            {
                Warning("No tags found. Skipping tag page generation.");
                return;
            }

            var templateContent = (TemplateDirectory / "template.html").ReadAllText();
            var menu = BuildMenu();

            var tagsDir = OutputDirectory / "tags";
            tagsDir.CreateDirectory();

            GenerateTagIndex(tagMap, templateContent, menu);
            GenerateIndividualTagPages(tagMap, templateContent, menu);

            Information("Tag pages generated: {Count} tags", tagMap.Count);
        });

    private Dictionary<string, List<TaggedPage>> CollectTags()
    {
        var tagMap = new Dictionary<string, List<TaggedPage>>(StringComparer.OrdinalIgnoreCase);
        var markdownFiles = InputDirectory.GlobFiles("**/*.md");

        foreach (var file in markdownFiles)
        {
            var content = file.ReadAllText();
            var pipeline = new MarkdownPipelineBuilder().UseYamlFrontMatter().Build();
            var document = Markdown.Parse(content, pipeline);
            var metadata = ExtractTagMetadata(document, content);

            if (!metadata.TryGetValue("keywords", out var keywordsStr) || string.IsNullOrWhiteSpace(keywordsStr))
                continue;

            var tags = keywordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var pageTitle = metadata.TryGetValue("title", out var title) ? title : file.NameWithoutExtension;
            var pageDate = metadata.TryGetValue("date", out var date) ? date : "";

            foreach (var tag in tags)
            {
                var normalizedTag = tag.ToLowerInvariant();
                if (!tagMap.ContainsKey(normalizedTag))
                    tagMap[normalizedTag] = new List<TaggedPage>();

                tagMap[normalizedTag].Add(new TaggedPage
                {
                    Title = pageTitle,
                    Url = $"/{file.NameWithoutExtension}.html",
                    Date = pageDate
                });
            }
        }

        return tagMap;
    }

    private void GenerateTagIndex(Dictionary<string, List<TaggedPage>> tagMap, string templateContent, List<TagMenuItem> menu)
    {
        var sortedTags = tagMap.OrderBy(t => t.Key).ToList();

        var tagListHtml = "<h1>Tags</h1>\n<div style=\"display: flex; flex-wrap: wrap; gap: 0.5rem;\">\n";
        foreach (var tag in sortedTags)
        {
            tagListHtml += $"  <a href=\"/tags/{Uri.EscapeDataString(tag.Key)}.html\" " +
                           $"style=\"display: inline-block; padding: 0.25rem 0.75rem; " +
                           $"border-radius: 1rem; text-decoration: none;\">" +
                           $"{tag.Key} ({tag.Value.Count})</a>\n";
        }
        tagListHtml += "</div>";

        var templateData = new
        {
            site_title = SiteTitle,
            page_title = "Tags",
            description = "Browse all tags",
            keywords = "",
            author = "",
            date = "",
            content = tagListHtml,
            page_url = $"{SiteBaseUrl.TrimEnd('/')}/tags.html",
            image_url = "",
            menu,
            tags = Array.Empty<object>()
        };

        var finalHtml = Template.Parse(templateContent).Render(templateData);
        var outputFile = OutputDirectory / "tags.html";
        outputFile.WriteAllText(finalHtml);

        Information("Generated tag index: {File}", outputFile);
    }

    private void GenerateIndividualTagPages(Dictionary<string, List<TaggedPage>> tagMap, string templateContent, List<TagMenuItem> menu)
    {
        foreach (var (tag, pages) in tagMap)
        {
            var sortedPages = pages.OrderByDescending(p => p.Date).ToList();

            var contentHtml = $"<h1>Posts tagged \"{tag}\"</h1>\n<ul>\n";
            foreach (var page in sortedPages)
            {
                var dateDisplay = !string.IsNullOrEmpty(page.Date) ? $" ({page.Date})" : "";
                contentHtml += $"  <li><a href=\"{page.Url}\">{page.Title}</a>{dateDisplay}</li>\n";
            }
            contentHtml += "</ul>\n";
            contentHtml += "<p><a href=\"/tags.html\">&larr; All tags</a></p>";

            var templateData = new
            {
                site_title = SiteTitle,
                page_title = $"Tag: {tag}",
                description = $"Posts tagged with {tag}",
                keywords = tag,
                author = "",
                date = "",
                content = contentHtml,
                page_url = $"{SiteBaseUrl.TrimEnd('/')}/tags/{Uri.EscapeDataString(tag)}.html",
                image_url = "",
                menu,
                tags = Array.Empty<object>()
            };

            var finalHtml = Template.Parse(templateContent).Render(templateData);
            var outputFile = OutputDirectory / "tags" / $"{tag}.html";
            outputFile.WriteAllText(finalHtml);

            Information("Generated tag page: {File}", outputFile);
        }
    }

    private List<TagMenuItem> BuildMenu()
    {
        var markdownFiles = InputDirectory.GlobFiles("**/*.md");
        var menu = markdownFiles
            .Select(file => new TagMenuItem
            {
                Title = file.NameWithoutExtension,
                Url = $"{file.NameWithoutExtension}.html"
            })
            .Where(item => item.Url != "index.html")
            .ToList();

        return menu;
    }

    private static Dictionary<string, string> ExtractTagMetadata(MarkdownDocument document, string content)
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

    record TaggedPage
    {
        public required string Title { get; init; }
        public required string Url { get; init; }
        public required string Date { get; init; }
    }

    record TagMenuItem
    {
        public required string Title { get; init; }
        public required string Url { get; init; }
    }
}
