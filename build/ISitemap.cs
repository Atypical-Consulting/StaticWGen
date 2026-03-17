using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

public interface ISitemap : IHasWebsitePaths
{
    Target GenerateSitemap => _ => _
        .DependsOn<IGenerateWebsite>(x => x.GenerateHtml)
        .TriggeredBy<IGenerateWebsite>(x => x.GenerateHtml)
        .Executes(() =>
        {
            Information("Generating enhanced sitemap.xml...");

            var baseUrl = SiteBaseUrl.TrimEnd('/');
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XNamespace image = "http://www.google.com/schemas/sitemap-image/1.1";

            var htmlFiles = OutputDirectory.GlobFiles("**/*.html")
                .Where(file => file.Name != "404.html")
                .ToList();

            var urlElements = new List<XElement>();

            foreach (var file in htmlFiles)
            {
                var relativePath = OutputDirectory.GetRelativePathTo(file).ToString().Replace('\\', '/');
                var pageUrl = new Uri(new Uri(baseUrl + "/"), relativePath).AbsoluteUri;
                var fileName = file.NameWithoutExtension;

                // Try to get metadata from matching input markdown file
                var metadata = TryGetMetadataForPage(fileName);

                // Determine lastmod
                var lastmod = GetLastModified(file, metadata);

                // Determine changefreq and priority based on page type
                var (changefreq, priority) = GetSitemapHints(fileName, relativePath, metadata);

                var urlElement = new XElement(ns + "url",
                    new XElement(ns + "loc", pageUrl),
                    new XElement(ns + "lastmod", lastmod),
                    new XElement(ns + "changefreq", changefreq),
                    new XElement(ns + "priority", priority.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                );

                // Add image entries
                var images = ExtractImages(file, baseUrl);
                foreach (var img in images)
                {
                    urlElement.Add(new XElement(image + "image",
                        new XElement(image + "loc", img.Url),
                        new XElement(image + "title", img.Title)
                    ));
                }

                urlElements.Add(urlElement);
            }

            var sitemap = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(ns + "urlset",
                    new XAttribute(XNamespace.Xmlns + "image", image.NamespaceName),
                    urlElements
                )
            );

            var sitemapPath = OutputDirectory / "sitemap.xml";
            sitemap.Save(sitemapPath);

            Information("Enhanced sitemap.xml generated at {SitemapPath} with {Count} URLs",
                sitemapPath, urlElements.Count);
        });

    private Dictionary<string, string> TryGetMetadataForPage(string fileName)
    {
        var mdFile = InputDirectory / $"{fileName}.md";
        if (mdFile.FileExists())
        {
            var (metadata, _) = MarkdownHelper.ParseMarkdownFile(mdFile);
            return metadata;
        }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetLastModified(AbsolutePath htmlFile, Dictionary<string, string> metadata)
    {
        // Use date from front-matter if available
        if (metadata.TryGetValue("date", out var dateStr) && DateTime.TryParse(dateStr, out var date))
            return date.ToString("yyyy-MM-dd");

        // Fall back to file modification time
        return File.GetLastWriteTimeUtc(htmlFile).ToString("yyyy-MM-dd");
    }

    private static (string Changefreq, double Priority) GetSitemapHints(
        string fileName, string relativePath, Dictionary<string, string> metadata)
    {
        // Check for front-matter overrides
        var changefreq = metadata.TryGetValue("sitemap_changefreq", out var cf) ? cf : null;
        var priority = metadata.TryGetValue("sitemap_priority", out var p) && double.TryParse(p, out var pv) ? pv : -1;

        // Auto-assign based on page type
        if (changefreq == null || priority < 0)
        {
            if (fileName == "index")
            {
                changefreq ??= "weekly";
                if (priority < 0) priority = 1.0;
            }
            else if (fileName == "tags" || relativePath.StartsWith("tags/"))
            {
                changefreq ??= "weekly";
                if (priority < 0) priority = fileName == "tags" ? 0.6 : 0.5;
            }
            else if (fileName == "blog" || relativePath.Contains("blog/page"))
            {
                changefreq ??= "weekly";
                if (priority < 0) priority = 0.6;
            }
            else if (metadata.ContainsKey("date"))
            {
                changefreq ??= "monthly";
                if (priority < 0) priority = 0.7;
            }
            else
            {
                changefreq ??= "yearly";
                if (priority < 0) priority = 0.8;
            }
        }

        return (changefreq!, priority);
    }

    private static List<SitemapImage> ExtractImages(AbsolutePath htmlFile, string baseUrl)
    {
        var images = new List<SitemapImage>();
        var content = htmlFile.ReadAllText();
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var imgNodes = doc.DocumentNode.SelectNodes("//article//img[@src]");
        if (imgNodes == null) return images;

        foreach (var img in imgNodes)
        {
            var src = img.GetAttributeValue("src", "");
            if (string.IsNullOrEmpty(src)) continue;

            // Resolve relative URLs
            var imgUrl = src.StartsWith("http") ? src : $"{baseUrl}/{src.TrimStart('/')}";
            var alt = img.GetAttributeValue("alt", Path.GetFileNameWithoutExtension(src));

            images.Add(new SitemapImage { Url = imgUrl, Title = alt });
        }

        return images;
    }

    record SitemapImage
    {
        public required string Url { get; init; }
        public required string Title { get; init; }
    }
}
