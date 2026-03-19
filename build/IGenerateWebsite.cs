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
    [Parameter("Default image URL for social sharing")]
    string DefaultImageUrl => TryGetValue(() => DefaultImageUrl) ?? "";
    
    [Parameter("Analytics provider: plausible, google, custom, or empty to disable")]
    string AnalyticsProvider => TryGetValue(() => AnalyticsProvider) ?? "";

    [Parameter("Analytics site ID (domain for Plausible, measurement ID for GA4)")]
    string AnalyticsSiteId => TryGetValue(() => AnalyticsSiteId) ?? "";

    [Parameter("Custom analytics script URL")]
    string AnalyticsScriptUrl => TryGetValue(() => AnalyticsScriptUrl) ?? "";

    [Parameter("Include draft pages in output")]
    bool IncludeDrafts => TryGetValue<bool?>(() => IncludeDrafts) ?? false;

    [Parameter("Continue processing remaining files when one fails")]
    bool ContinueOnError => TryGetValue<bool?>(() => ContinueOnError) ?? false;

    Target GenerateHtml => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            Information("Generating HTML files from Markdown...");

            var template = TemplateDirectory / "template.html";
            var templateContent = template.ReadAllText();

            // Step 1: Get all Markdown files and filter by content status
            var markdownFiles = InputDirectory.GlobFiles("**/*.md");
            var publishableFiles = new List<AbsolutePath>();
            var draftCount = 0;
            var scheduledCount = 0;
            var archivedCount = 0;

            foreach (var file in markdownFiles)
            {
                var (metadata, _) = MarkdownHelper.ParseMarkdownFile(file);
                var status = MarkdownHelper.GetContentStatus(metadata, IncludeDrafts);

                switch (status)
                {
                    case ContentStatus.Excluded:
                        if (metadata.TryGetValue("draft", out var isDraft) &&
                            string.Equals(isDraft, "true", StringComparison.OrdinalIgnoreCase))
                        {
                            draftCount++;
                            Information("Skipping draft: {File}", file.Name);
                        }
                        else if (metadata.TryGetValue("publishDate", out var pubDate))
                        {
                            scheduledCount++;
                            Information("Skipping scheduled post: {File} (publishes {Date})", file.Name, pubDate);
                        }
                        continue;
                    case ContentStatus.Draft:
                        draftCount++;
                        Information("Including draft (--include-drafts): {File}", file.Name);
                        break;
                    case ContentStatus.Scheduled:
                        scheduledCount++;
                        Information("Including scheduled post (--include-drafts): {File}", file.Name);
                        break;
                    case ContentStatus.Archived:
                        archivedCount++;
                        break;
                }

                publishableFiles.Add(file);
            }

            // Step 2: Generate menu from publishable, non-archived files
            var menuFiles = publishableFiles
                .Where(f =>
                {
                    var (m, _) = MarkdownHelper.ParseMarkdownFile(f);
                    return MarkdownHelper.GetContentStatus(m) != ContentStatus.Archived;
                })
                .ToList();
            var menu = GenerateMenu(menuFiles);

            // Step 3: Generate HTML for each publishable file with error tracking
            var successCount = 0;
            var errorCount = 0;
            var errors = new List<string>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var file in publishableFiles)
            {
                try
                {
                    ProcessMarkdownFile(file, templateContent, menu);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    var errorMsg = $"{file.Name}: {ex.Message}";
                    errors.Add(errorMsg);
                    Error("Error processing {File}: {Message}", file.Name, ex.Message);

                    if (!ContinueOnError)
                        throw;
                }
            }

            stopwatch.Stop();

            // Build Summary
            Information("═══════════════════════════════════════");
            Information("  Build Summary");
            Information("═══════════════════════════════════════");
            Information("  Processed: {Count} pages ({Time:F1}s)", successCount, stopwatch.Elapsed.TotalSeconds);
            if (draftCount > 0)
                Information("  Drafts:    {Count} pages {Status}", draftCount,
                    IncludeDrafts ? "(included)" : "(skipped)");
            if (scheduledCount > 0)
                Information("  Scheduled: {Count} pages {Status}", scheduledCount,
                    IncludeDrafts ? "(included)" : "(skipped)");
            if (archivedCount > 0)
                Information("  Archived:  {Count} pages", archivedCount);
            if (errorCount > 0)
            {
                Error("  Errors:    {Count}", errorCount);
                foreach (var err in errors)
                    Error("    - {Error}", err);
            }
            Information("═══════════════════════════════════════");

            if (errorCount > 0 && ContinueOnError)
                Assert.Fail($"Build completed with {errorCount} error(s). See summary above.");
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
            .Select(file =>
            {
                var slug = file.NameWithoutExtension;
                var (metadata, _) = MarkdownHelper.ParseMarkdownFile(file);
                var title = metadata.TryGetValue("title", out var t) && !string.IsNullOrWhiteSpace(t)
                    ? t
                    : slug;
                var hidden = metadata.TryGetValue("menu", out var m)
                             && m.Equals("false", StringComparison.OrdinalIgnoreCase);
                return new MenuItem
                {
                    Title = title,
                    Url = $"{slug}.html",
                    Hidden = hidden
                };
            })
            // exclude index.html, 404.html, translation files, date-prefixed posts, and pages with menu: false
            .Where(item => item.Url != "index.html" && item.Url != "404.html")
            .Where(item => !item.Url.Replace(".html", "").Contains('.'))
            .Where(item => !System.Text.RegularExpressions.Regex.IsMatch(item.Url, @"^\d{4}-\d{2}-\d{2}"))
            .Where(item => !item.Hidden)
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

            // Apply content transformer plugins
            htmlContent = ContentTransformerPipeline.Apply(htmlContent, metadata);

            // Generate Table of Contents
            var tocHtml = GenerateTableOfContents(htmlContent, metadata);

            // Build hreflang links for translations
            var hreflangLinks = BuildHreflangLinks(file, metadata);

            // Prepare data for the template
            var templateData = PrepareTemplateData(file, metadata, htmlContent, tocHtml, menu, hreflangLinks);

            // Replace placeholders in the template
            var finalHtml = Template
                .Parse(templateContent)
                .Render(templateData);

            // Determine output path based on language
            // Only translation files (e.g., about.fr.md) go to /{lang}/ subdirectory
            var lang = metadata.TryGetValue("lang", out var l) ? l : "";
            var isTranslationFile = !string.IsNullOrEmpty(lang) &&
                                    metadata.ContainsKey("translationOf") &&
                                    file.NameWithoutExtension.Contains('.');
            AbsolutePath outputFile;
            if (isTranslationFile)
            {
                var langDir = OutputDirectory / lang;
                langDir.CreateDirectory();
                var baseName = metadata["translationOf"];
                outputFile = langDir / $"{baseName}.html";
            }
            else
            {
                outputFile = OutputDirectory / $"{file.NameWithoutExtension}.html";
            }
            outputFile.WriteAllText(finalHtml);

            Information($"Generated HTML: {outputFile}");
        }
        catch (Exception ex)
        {
            Error($"Error processing {file}: {ex.Message}");
            throw;
        }
    }

    private object PrepareTemplateData(AbsolutePath file, Dictionary<string, string> metadata, string htmlContent, string tocHtml, List<MenuItem> menu, List<HreflangLink> hreflangLinks)
    {
        var lang = metadata.TryGetValue("lang", out var l) ? l : "en";
        var baseName = metadata.TryGetValue("translationOf", out var tOf) ? tOf : file.NameWithoutExtension;
        var pagePath = !string.IsNullOrEmpty(l) ? $"{l}/{baseName}.html" : $"{file.NameWithoutExtension}.html";
        var pageUrl = new Uri(new Uri(SiteBaseUrl.TrimEnd('/') + "/"), pagePath).AbsoluteUri;

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
            analytics_snippet = IncludeDrafts ? "" : GenerateAnalyticsSnippet(),
            lang,
            hreflang_links = hreflangLinks,
            menu,
            tags
        };

        return templateData;
    }

    private List<HreflangLink> BuildHreflangLinks(AbsolutePath file, Dictionary<string, string> metadata)
    {
        var links = new List<HreflangLink>();
        var translationOf = metadata.TryGetValue("translationOf", out var tOf) ? tOf : "";
        if (string.IsNullOrEmpty(translationOf))
            return links;

        var baseUrl = SiteBaseUrl.TrimEnd('/');
        var allFiles = InputDirectory.GlobFiles("**/*.md");

        foreach (var f in allFiles)
        {
            var (m, _) = MarkdownHelper.ParseMarkdownFile(f);
            var fTranslation = m.TryGetValue("translationOf", out var ft) ? ft : "";
            if (fTranslation != translationOf)
                continue;

            var fLang = m.TryGetValue("lang", out var fl) ? fl : "en";
            var href = $"{baseUrl}/{fLang}/{translationOf}.html";
            links.Add(new HreflangLink { Lang = fLang, Href = href });
        }

        // Add x-default pointing to default language
        if (links.Count > 0)
        {
            var defaultLink = links.FirstOrDefault(l => l.Lang == "en") ?? links.First();
            links.Add(new HreflangLink { Lang = "x-default", Href = defaultLink.Href });
        }

        return links;
    }

    record HreflangLink
    {
        public required string Lang { get; init; }
        public required string Href { get; init; }
    }

    private string GenerateAnalyticsSnippet()
    {
        if (string.IsNullOrEmpty(AnalyticsProvider))
            return "";

        return AnalyticsProvider.ToLowerInvariant() switch
        {
            "plausible" => $"<script defer data-domain=\"{AnalyticsSiteId}\" " +
                           $"src=\"{(string.IsNullOrEmpty(AnalyticsScriptUrl) ? "https://plausible.io/js/script.js" : AnalyticsScriptUrl)}\"></script>",

            "google" or "ga4" => $"<script async src=\"https://www.googletagmanager.com/gtag/js?id={AnalyticsSiteId}\"></script>\n" +
                                 "<script>\n" +
                                 "  window.dataLayer = window.dataLayer || [];\n" +
                                 "  function gtag(){dataLayer.push(arguments);}\n" +
                                 "  gtag('js', new Date());\n" +
                                 $"  gtag('config', '{AnalyticsSiteId}');\n" +
                                 "</script>",

            "custom" => !string.IsNullOrEmpty(AnalyticsScriptUrl)
                ? $"<script defer src=\"{AnalyticsScriptUrl}\"></script>"
                : "",

            _ => ""
        };
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
        public bool Hidden { get; init; }
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
