using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Scriban;
using static Serilog.Log;

public interface IGenerateTagPages : IHasWebsitePaths
{
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
            var (metadata, _) = MarkdownHelper.ParseMarkdownFile(file);

            // Skip drafts, scheduled, excluded, and translation files
            var status = MarkdownHelper.GetContentStatus(metadata);
            if (status == ContentStatus.Excluded)
                continue;
            if (metadata.ContainsKey("translationOf") && file.NameWithoutExtension.Contains('.'))
                continue;

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
                    Url = $"{BasePath}/{file.NameWithoutExtension}.html",
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
            tagListHtml += $"  <a href=\"{BasePath}/tags/{Uri.EscapeDataString(tag.Key)}.html\" " +
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
            base_path = BasePath,
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
        var postsPerPage = 10;

        foreach (var (tag, pages) in tagMap)
        {
            var sortedPages = pages.OrderByDescending(p => p.Date).ToList();
            var totalPages = (int)Math.Ceiling((double)sortedPages.Count / postsPerPage);

            for (var page = 1; page <= totalPages; page++)
            {
                var pagePosts = sortedPages
                    .Skip((page - 1) * postsPerPage)
                    .Take(postsPerPage)
                    .ToList();

                var contentHtml = $"<h1>Posts tagged \"{tag}\"{(page > 1 ? $" — Page {page}" : "")}</h1>\n<ul>\n";
                foreach (var post in pagePosts)
                {
                    var dateDisplay = !string.IsNullOrEmpty(post.Date) ? $" ({post.Date})" : "";
                    contentHtml += $"  <li><a href=\"{post.Url}\">{post.Title}</a>{dateDisplay}</li>\n";
                }
                contentHtml += "</ul>\n";

                // Pagination nav
                if (totalPages > 1)
                {
                    var basePath = $"{BasePath}/tags/{Uri.EscapeDataString(tag)}";
                    var prevUrl = page > 1
                        ? (page == 2 ? $"{basePath}.html" : $"{basePath}/page/{page - 1}.html")
                        : "";
                    var nextUrl = page < totalPages ? $"{basePath}/page/{page + 1}.html" : "";

                    contentHtml += "<nav aria-label=\"Pagination\" style=\"margin-top: 1rem;\">\n";
                    contentHtml += "  <ul style=\"display: flex; justify-content: center; gap: 1rem; list-style: none; padding: 0;\">\n";
                    contentHtml += !string.IsNullOrEmpty(prevUrl)
                        ? $"    <li><a href=\"{prevUrl}\">&laquo; Previous</a></li>\n"
                        : "    <li><span aria-disabled=\"true\" style=\"opacity: 0.5;\">&laquo; Previous</span></li>\n";
                    contentHtml += $"    <li>Page {page} of {totalPages}</li>\n";
                    contentHtml += !string.IsNullOrEmpty(nextUrl)
                        ? $"    <li><a href=\"{nextUrl}\">Next &raquo;</a></li>\n"
                        : "    <li><span aria-disabled=\"true\" style=\"opacity: 0.5;\">Next &raquo;</span></li>\n";
                    contentHtml += "  </ul>\n</nav>\n";
                }

                contentHtml += $"<p><a href=\"{BasePath}/tags.html\">&larr; All tags</a></p>";

                var escapedTag = Uri.EscapeDataString(tag);
                var pageUrl = page == 1
                    ? $"{SiteBaseUrl.TrimEnd('/')}/tags/{escapedTag}.html"
                    : $"{SiteBaseUrl.TrimEnd('/')}/tags/{escapedTag}/page/{page}.html";

                var templateData = new
                {
                    site_title = SiteTitle,
                    page_title = page == 1 ? $"Tag: {tag}" : $"Tag: {tag} — Page {page}",
                    description = $"Posts tagged with {tag}",
                    keywords = tag,
                    author = "",
                    date = "",
                    iso_date = "",
                    og_type = "website",
                    schema_type = "WebPage",
                    content = contentHtml,
                    toc = "",
                    page_url = pageUrl,
                    canonical_url = pageUrl,
                    image_url = "",
                    base_path = BasePath,
                    menu,
                    tags = Array.Empty<object>()
                };

                var finalHtml = Template.Parse(templateContent).Render(templateData);

                if (page == 1)
                {
                    var outputFile = OutputDirectory / "tags" / $"{tag}.html";
                    outputFile.WriteAllText(finalHtml);
                    Information("Generated tag page: {File}", outputFile);
                }
                else
                {
                    var pageDir = OutputDirectory / "tags" / tag / "page";
                    pageDir.CreateDirectory();
                    var outputFile = pageDir / $"{page}.html";
                    outputFile.WriteAllText(finalHtml);
                    Information("Generated tag page {Tag} page {Page}: {File}", tag, page, outputFile);
                }
            }
        }
    }

    private List<TagMenuItem> BuildMenu()
    {
        var markdownFiles = InputDirectory.GlobFiles("**/*.md");
        var datePattern = new System.Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2}");
        var menu = markdownFiles
            .Where(file =>
            {
                var (m, _) = MarkdownHelper.ParseMarkdownFile(file);
                return MarkdownHelper.GetContentStatus(m) != ContentStatus.Excluded;
            })
            .Select(file =>
            {
                var slug = file.NameWithoutExtension;
                var (metadata, _) = MarkdownHelper.ParseMarkdownFile(file);
                var title = metadata.TryGetValue("title", out var t) && !string.IsNullOrWhiteSpace(t)
                    ? t
                    : slug;
                var hidden = metadata.TryGetValue("menu", out var m)
                             && m.Equals("false", StringComparison.OrdinalIgnoreCase);
                return new TagMenuItem
                {
                    Title = title,
                    Url = $"{slug}.html",
                    Hidden = hidden
                };
            })
            .Where(item => item.Url != "index.html" && item.Url != "404.html")
            .Where(item => !item.Url.Replace(".html", "").Contains('.'))
            .Where(item => !datePattern.IsMatch(item.Url))
            .Where(item => !item.Hidden)
            .ToList();

        return menu;
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
        public bool Hidden { get; init; }
    }
}
