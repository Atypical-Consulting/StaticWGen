using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Scriban;
using static Serilog.Log;

public interface IGenerateBlogIndex : IHasWebsitePaths
{
    [Parameter("Title of the site")][Required]
    string SiteTitle => TryGetValue(() => SiteTitle);

    int PostsPerPage => 10;

    Target GenerateBlogIndex => _ => _
        .DependsOn<IGenerateWebsite>(x => x.GenerateHtml)
        .TriggeredBy<IGenerateWebsite>(x => x.GenerateHtml)
        .Executes(() =>
        {
            Information("Generating paginated blog index...");

            var posts = CollectPosts();
            if (posts.Count == 0)
            {
                Warning("No dated posts found. Skipping blog index generation.");
                return;
            }

            var templateContent = (TemplateDirectory / "template.html").ReadAllText();
            var menu = BuildIndexMenu();
            var totalPages = (int)Math.Ceiling((double)posts.Count / PostsPerPage);

            for (var page = 1; page <= totalPages; page++)
            {
                var pagePosts = posts
                    .Skip((page - 1) * PostsPerPage)
                    .Take(PostsPerPage)
                    .ToList();

                var contentHtml = $"<h1>Blog{(page > 1 ? $" — Page {page}" : "")}</h1>\n";
                foreach (var post in pagePosts)
                {
                    contentHtml += "<article style=\"margin-bottom: 1.5rem;\">\n";
                    contentHtml += $"  <h2><a href=\"{post.Url}\">{post.Title}</a></h2>\n";
                    if (!string.IsNullOrEmpty(post.Date))
                        contentHtml += $"  <small><time datetime=\"{post.Date}\">{post.Date}</time></small>\n";
                    if (!string.IsNullOrEmpty(post.Description))
                        contentHtml += $"  <p>{post.Description}</p>\n";
                    contentHtml += "</article>\n";
                }

                contentHtml += BuildPaginationNav(page, totalPages, "/blog");

                var pageUrl = page == 1
                    ? $"{SiteBaseUrl.TrimEnd('/')}/blog.html"
                    : $"{SiteBaseUrl.TrimEnd('/')}/blog/page/{page}.html";

                var templateData = new
                {
                    site_title = SiteTitle,
                    page_title = page == 1 ? "Blog" : $"Blog — Page {page}",
                    description = "Blog posts",
                    keywords = "",
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
                    menu,
                    tags = Array.Empty<object>()
                };

                var finalHtml = Template.Parse(templateContent).Render(templateData);

                if (page == 1)
                {
                    var outputFile = OutputDirectory / "blog.html";
                    outputFile.WriteAllText(finalHtml);
                    Information("Generated blog index: {File}", outputFile);
                }
                else
                {
                    var pageDir = OutputDirectory / "blog" / "page";
                    pageDir.CreateDirectory();
                    var outputFile = pageDir / $"{page}.html";
                    outputFile.WriteAllText(finalHtml);
                    Information("Generated blog page {Page}: {File}", page, outputFile);
                }
            }

            Information("Blog index generated: {PostCount} posts across {PageCount} pages",
                posts.Count, totalPages);
        });

    private List<BlogPost> CollectPosts()
    {
        var markdownFiles = InputDirectory.GlobFiles("**/*.md");
        var posts = new List<BlogPost>();

        foreach (var file in markdownFiles)
        {
            var (metadata, _) = MarkdownHelper.ParseMarkdownFile(file);

            // Skip drafts, scheduled, and excluded content
            var status = MarkdownHelper.GetContentStatus(metadata);
            if (status == ContentStatus.Excluded)
                continue;

            if (!metadata.TryGetValue("date", out var dateStr) || string.IsNullOrEmpty(dateStr))
                continue;

            posts.Add(new BlogPost
            {
                Title = metadata.TryGetValue("title", out var title) ? title : file.NameWithoutExtension,
                Url = $"/{file.NameWithoutExtension}.html",
                Date = dateStr,
                Description = metadata.TryGetValue("description", out var desc) ? desc : ""
            });
        }

        return posts.OrderByDescending(p => p.Date).ToList();
    }

    static string BuildPaginationNav(int currentPage, int totalPages, string basePath)
    {
        if (totalPages <= 1)
            return "";

        var prevUrl = currentPage > 1
            ? (currentPage == 2 ? $"{basePath}.html" : $"{basePath}/page/{currentPage - 1}.html")
            : "";
        var nextUrl = currentPage < totalPages
            ? $"{basePath}/page/{currentPage + 1}.html"
            : "";

        var nav = "<nav aria-label=\"Pagination\" style=\"margin-top: 2rem;\">\n";
        nav += "  <ul style=\"display: flex; justify-content: center; gap: 1rem; list-style: none; padding: 0;\">\n";

        if (!string.IsNullOrEmpty(prevUrl))
            nav += $"    <li><a href=\"{prevUrl}\">&laquo; Previous</a></li>\n";
        else
            nav += "    <li><span aria-disabled=\"true\" style=\"opacity: 0.5;\">&laquo; Previous</span></li>\n";

        nav += $"    <li>Page {currentPage} of {totalPages}</li>\n";

        if (!string.IsNullOrEmpty(nextUrl))
            nav += $"    <li><a href=\"{nextUrl}\">Next &raquo;</a></li>\n";
        else
            nav += "    <li><span aria-disabled=\"true\" style=\"opacity: 0.5;\">Next &raquo;</span></li>\n";

        nav += "  </ul>\n</nav>\n";
        return nav;
    }

    private List<IndexMenuItem> BuildIndexMenu()
    {
        var markdownFiles = InputDirectory.GlobFiles("**/*.md");
        return markdownFiles
            .Where(file =>
            {
                var (m, _) = MarkdownHelper.ParseMarkdownFile(file);
                return MarkdownHelper.GetContentStatus(m) != ContentStatus.Excluded;
            })
            .Select(file => new IndexMenuItem
            {
                Title = file.NameWithoutExtension,
                Url = $"{file.NameWithoutExtension}.html"
            })
            .Where(item => item.Url != "index.html" && item.Url != "404.html")
            .ToList();
    }

    record BlogPost
    {
        public required string Title { get; init; }
        public required string Url { get; init; }
        public required string Date { get; init; }
        public required string Description { get; init; }
    }

    record IndexMenuItem
    {
        public required string Title { get; init; }
        public required string Url { get; init; }
    }
}
