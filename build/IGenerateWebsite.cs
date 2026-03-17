using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Prism;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;
using Scriban;
using YamlDotNet.RepresentationModel;
using static Serilog.Log;

public interface IGenerateWebsite : IHasWebsitePaths
{
    [Parameter("Title of the site")][Required]
    string SiteTitle => TryGetValue(() => SiteTitle);
    
    [Parameter("Default image URL for social sharing")]
    string DefaultImageUrl => TryGetValue(() => DefaultImageUrl) ?? "";
    
    Target GenerateHtml => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            Information("Generating HTML files from Markdown...");
            
            var template = TemplateDirectory / "template.html";
            var templateContent = template.ReadAllText();
            
            // Step 1: Get all Markdown files and generate menu dynamically
            var markdownFiles = InputDirectory.GlobFiles("**/*.md");
            var menu = GenerateMenu(markdownFiles);
            
            // Step 2: Generate HTML for each Markdown file
            markdownFiles.ForEach(file => ProcessMarkdownFile(file, templateContent, menu));
            
            Information("HTML files generated successfully!");
        });

    Target CopyAssets => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            var inputAssets = InputDirectory / "assets";
            var outputAssets = OutputDirectory / "assets";
            CopyDirectory(inputAssets, outputAssets, "Assets");
        });
    
    Target CopyJsScripts => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            var templateScripts = TemplateDirectory / "js";
            var outputScripts = OutputDirectory / "js";
            CopyDirectory(templateScripts, outputScripts, "Scripts");
        });
    
    Target CopyCss => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            var templateCss = TemplateDirectory / "css";
            var outputCss = OutputDirectory / "css";
            CopyDirectory(templateCss, outputCss, "CSS");
        });
    
    Target BuildWebsite => _ => _
        .DependsOn(CopyAssets, CopyJsScripts, CopyCss)
        .DependsOn<ISitemap>(x => x.GenerateSitemap)
        .DependsOn<IRobotsTxt>(x => x.GenerateRobotsTxt)
        .DependsOn<IGenerateFeed>(x => x.GenerateFeed)
        .DependsOn<IGenerateTagPages>(x => x.GenerateTagPages)
        .DependsOn<IGenerateBlogIndex>(x => x.GenerateBlogIndex)
        .Executes(() =>
        {
            // Add more logic if necessary, like bundling, minifying, etc.
            Information("Static website built successfully!");
        });
    
    private static List<MenuItem> GenerateMenu(IReadOnlyCollection<AbsolutePath> markdownFiles)
    {
        var menu = markdownFiles
            .Select(file => new MenuItem
            {
                Title = file.NameWithoutExtension,
                Url = $"{file.NameWithoutExtension}.html"
            })
            // exclude index.html and 404.html from the menu
            .Where(item => item.Url != "index.html" && item.Url != "404.html")
            .ToList();
        
        return menu;
    }

    private void ProcessMarkdownFile(AbsolutePath file, string templateContent, List<MenuItem> menu)
    {
        try
        {
            Information($"Processing {file}");

            var content = file.ReadAllText();

            // Use shared pipeline with all extensions
            var markdownPipeline = MarkdownHelper.CreatePipeline();

            // Register custom code block renderer that handles Mermaid and Prism together
            var writer = new StringWriter();
            var htmlRenderer = new HtmlRenderer(writer);
            markdownPipeline.Setup(htmlRenderer);
            var defaultCodeBlockRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();
            if (defaultCodeBlockRenderer != null)
                htmlRenderer.ObjectRenderers.Remove(defaultCodeBlockRenderer);
            htmlRenderer.ObjectRenderers.AddIfNotAlready(new MermaidAwareCodeBlockRenderer());

            // Parse the Markdown document
            var markdownDocument = Markdown.Parse(content, markdownPipeline);

            // Extract YAML front matter
            var metadata = MarkdownHelper.ExtractMetadata(markdownDocument, content, file.Name);

            // Remove the YAML front matter from the content
            var markdownContent = MarkdownHelper.RemoveFrontMatter(markdownDocument, content);

            // Convert Markdown to HTML using custom renderer
            var markdownDoc2 = Markdown.Parse(markdownContent, markdownPipeline);
            htmlRenderer.Render(markdownDoc2);
            writer.Flush();
            var htmlContent = writer.ToString();

            // Generate Table of Contents
            var tocHtml = GenerateTableOfContents(htmlContent, metadata);

            // Prepare data for the template
            var templateData = PrepareTemplateData(file, metadata, htmlContent, tocHtml, menu);

            // Replace placeholders in the template
            var finalHtml = Template
                .Parse(templateContent)
                .Render(templateData);

            var outputFile = OutputDirectory / $"{file.NameWithoutExtension}.html";
            outputFile.WriteAllText(finalHtml);

            Information($"Generated HTML: {outputFile}");
        }
        catch (Exception ex)
        {
            Error($"Error processing {file}: {ex.Message}");
            throw;
        }
    }

    private object PrepareTemplateData(AbsolutePath file, Dictionary<string, string> metadata, string htmlContent, string tocHtml, List<MenuItem> menu)
    {
        var pageUrl = new Uri(new Uri(SiteBaseUrl.TrimEnd('/') + "/"), $"{file.NameWithoutExtension}.html").AbsoluteUri;

        var tags = new List<TagLink>();
        if (metadata.TryGetValue("keywords", out var keywordsStr) && !string.IsNullOrWhiteSpace(keywordsStr))
        {
            tags = keywordsStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => new TagLink { Name = t.ToLowerInvariant(), Url = $"/tags/{Uri.EscapeDataString(t.ToLowerInvariant())}.html" })
                .ToList();
        }

        var pageTitle = metadata.TryGetValue("title", out var title) ? title : file.NameWithoutExtension;
        var pageDescription = metadata.TryGetValue("description", out var description) ? description : "";
        var pageAuthor = metadata.TryGetValue("author", out var author) ? author : "";
        var pageDate = metadata.TryGetValue("date", out var date) ? date : "";
        var pageImage = metadata.TryGetValue("image", out var image) ? image : DefaultImageUrl;

        // Determine content type based on presence of date metadata
        var hasDate = !string.IsNullOrEmpty(pageDate);
        var ogType = hasDate ? "article" : "website";
        var schemaType = hasDate ? "BlogPosting" : "WebPage";

        // Format date for ISO 8601 (JSON-LD / article:published_time)
        var isoDate = "";
        if (hasDate && DateTime.TryParse(pageDate, out var parsedDate))
            isoDate = parsedDate.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Check for noindex flag
        var isNoIndex = metadata.TryGetValue("noindex", out var noindex) &&
                        string.Equals(noindex, "true", StringComparison.OrdinalIgnoreCase);

        var templateData = new
        {
            site_title = SiteTitle,
            page_title = pageTitle,
            description = pageDescription,
            keywords = keywordsStr ?? "",
            author = pageAuthor,
            date = pageDate,
            iso_date = isoDate,
            og_type = ogType,
            schema_type = schemaType,
            noindex = isNoIndex,
            content = htmlContent,
            toc = tocHtml,
            page_url = pageUrl,
            canonical_url = pageUrl,
            image_url = pageImage,
            menu,
            tags
        };

        return templateData;
    }

    private static string GenerateTableOfContents(string htmlContent, Dictionary<string, string> metadata)
    {
        // Check if TOC is explicitly disabled
        if (metadata.TryGetValue("toc", out var tocSetting) &&
            string.Equals(tocSetting, "false", StringComparison.OrdinalIgnoreCase))
            return "";

        // Get max depth (default: 3 = h2-h4)
        var maxDepth = 3;
        if (metadata.TryGetValue("toc_depth", out var depthStr) && int.TryParse(depthStr, out var depth))
            maxDepth = Math.Clamp(depth, 1, 4);

        var minLevel = 2; // Start from h2
        var maxLevel = minLevel + maxDepth - 1; // e.g., h2-h4 for depth=3

        // Parse headings from HTML
        var headingPattern = $@"<h([{minLevel}-{maxLevel}])\s+id=""([^""]+)""[^>]*>(.*?)</h\1>";
        var matches = Regex.Matches(htmlContent, headingPattern, RegexOptions.Singleline);

        if (matches.Count < 3 && !string.Equals(tocSetting, "true", StringComparison.OrdinalIgnoreCase))
            return ""; // Auto-mode: skip TOC for pages with fewer than 3 headings

        if (matches.Count == 0)
            return "";

        var tocBuilder = new System.Text.StringBuilder();
        tocBuilder.AppendLine("<nav class=\"table-of-contents\" aria-label=\"Table of contents\">");
        tocBuilder.AppendLine("  <details open>");
        tocBuilder.AppendLine("    <summary>Contents</summary>");

        var currentLevel = minLevel;
        var listsOpen = 0;

        tocBuilder.AppendLine("    <ul>");
        listsOpen++;

        foreach (Match match in matches)
        {
            var level = int.Parse(match.Groups[1].Value);
            var id = match.Groups[2].Value;
            var text = Regex.Replace(match.Groups[3].Value, "<[^>]+>", "").Trim(); // Strip inner HTML tags

            while (level > currentLevel)
            {
                tocBuilder.AppendLine("      <ul>");
                listsOpen++;
                currentLevel++;
            }
            while (level < currentLevel)
            {
                tocBuilder.AppendLine("      </ul>");
                listsOpen--;
                currentLevel--;
            }

            tocBuilder.AppendLine($"      <li><a href=\"#{id}\">{text}</a></li>");
        }

        while (listsOpen > 0)
        {
            tocBuilder.AppendLine("    </ul>");
            listsOpen--;
        }

        tocBuilder.AppendLine("  </details>");
        tocBuilder.AppendLine("</nav>");

        return tocBuilder.ToString();
    }

    record TagLink
    {
        public required string Name { get; init; }
        public required string Url { get; init; }
    }
    
    private void CopyDirectory(AbsolutePath source, AbsolutePath destination, string description)
    {
        Information($"Copying {description.ToLower()}...");
        Information($"Source: {source}");
        Information($"Destination: {destination}");
        
        // Check if the source directory exists before attempting to copy
        if (source.DirectoryExists())
        {
            source.Copy(destination);
            Information($"{description} copied successfully!");
        }
        else
        {
            Warning($"{description} directory not found: {source}");
        }
    }
    
    record MenuItem
    {
        public required string Title { get; init; }
        public required string Url { get; init; }
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
                // Render mermaid blocks without <code> wrapper so Mermaid.js can process them
                renderer.Write("<pre class=\"mermaid\">");
                renderer.WriteLeafRawLines(obj, true, true);
                renderer.WriteLine("</pre>");
            }
            else
            {
                // Render other code blocks with Prism-compatible language class
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
