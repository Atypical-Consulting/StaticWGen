using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HtmlAgilityPack;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

public interface IValidateOutput : IHasWebsitePaths
{
    Target Validate => _ => _
        .DependsOn<IGenerateWebsite>(x => x.BuildWebsite)
        .TriggeredBy<IGenerateWebsite>(x => x.BuildWebsite)
        .Executes(() =>
        {
            Information("Validating generated output...");

            var htmlFiles = OutputDirectory.GlobFiles("**/*.html");
            var errors = new List<string>();
            var warnings = new List<string>();

            foreach (var file in htmlFiles)
            {
                var relativePath = OutputDirectory.GetRelativePathTo(file);
                var content = file.ReadAllText();
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                ValidateHtmlStructure(doc, relativePath.ToString(), errors, warnings);
                ValidateAccessibility(doc, relativePath.ToString(), errors, warnings);
                ValidateSeoMeta(doc, relativePath.ToString(), errors, warnings);
                ValidateInternalLinks(doc, relativePath.ToString(), htmlFiles, errors, warnings);
            }

            ValidateSitemapCoverage(htmlFiles, warnings);
            ValidateRobotsTxt(warnings);

            // Report results
            foreach (var warning in warnings)
                Warning(warning);

            foreach (var error in errors)
                Error(error);

            Information("Validation complete: {ErrorCount} error(s), {WarningCount} warning(s)",
                errors.Count, warnings.Count);

            if (errors.Count > 0)
                Assert.Fail($"Validation failed with {errors.Count} error(s). See output above.");
        });

    private static void ValidateHtmlStructure(HtmlDocument doc, string file, List<string> errors, List<string> warnings)
    {
        // Check for parse errors (unclosed tags, etc.)
        foreach (var error in doc.ParseErrors)
        {
            warnings.Add($"[HTML] {file}: Line {error.Line} - {error.Reason}");
        }

        // Check DOCTYPE
        var doctype = doc.DocumentNode.ChildNodes.FirstOrDefault(n => n.NodeType == HtmlNodeType.Document || n.Name == "#document");
        var html = doc.DocumentNode.SelectSingleNode("//html");
        if (html == null)
        {
            errors.Add($"[HTML] {file}: Missing <html> element");
        }

        // Check for <head> and <body>
        var head = doc.DocumentNode.SelectSingleNode("//head");
        var body = doc.DocumentNode.SelectSingleNode("//body");
        if (head == null)
            errors.Add($"[HTML] {file}: Missing <head> element");
        if (body == null)
            errors.Add($"[HTML] {file}: Missing <body> element");
    }

    private static void ValidateAccessibility(HtmlDocument doc, string file, List<string> errors, List<string> warnings)
    {
        // Check all <img> tags have alt attributes
        var images = doc.DocumentNode.SelectNodes("//img");
        if (images != null)
        {
            foreach (var img in images)
            {
                var alt = img.GetAttributeValue("alt", null);
                if (string.IsNullOrEmpty(alt))
                {
                    var src = img.GetAttributeValue("src", "unknown");
                    errors.Add($"[A11Y] {file}: Image missing alt text: {src}");
                }
            }
        }

        // Check heading hierarchy (no skipping levels)
        var headings = doc.DocumentNode.SelectNodes("//h1 | //h2 | //h3 | //h4 | //h5 | //h6");
        if (headings != null && headings.Count > 1)
        {
            var levels = headings.Select(h => int.Parse(h.Name.Substring(1))).ToList();
            for (int i = 1; i < levels.Count; i++)
            {
                if (levels[i] > levels[i - 1] + 1)
                {
                    warnings.Add($"[A11Y] {file}: Heading level skipped from h{levels[i - 1]} to h{levels[i]}");
                }
            }
        }

        // Check links have descriptive text (not "click here")
        var links = doc.DocumentNode.SelectNodes("//a");
        if (links != null)
        {
            var vagueLinkTexts = new[] { "click here", "here", "link", "read more" };
            foreach (var link in links)
            {
                var text = link.InnerText.Trim().ToLowerInvariant();
                if (vagueLinkTexts.Contains(text))
                {
                    warnings.Add($"[A11Y] {file}: Link has non-descriptive text: \"{link.InnerText.Trim()}\"");
                }
            }
        }
    }

    private static void ValidateSeoMeta(HtmlDocument doc, string file, List<string> errors, List<string> warnings)
    {
        // Check <title> tag
        var title = doc.DocumentNode.SelectSingleNode("//title");
        if (title == null || string.IsNullOrWhiteSpace(title.InnerText))
            errors.Add($"[SEO] {file}: Missing or empty <title> tag");

        // Check meta description
        var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
        if (metaDesc == null)
            warnings.Add($"[SEO] {file}: Missing <meta name=\"description\">");

        // Check Open Graph tags
        var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
        var ogDesc = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']");
        var ogImage = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");

        if (ogTitle == null)
            warnings.Add($"[SEO] {file}: Missing og:title meta tag");
        if (ogDesc == null)
            warnings.Add($"[SEO] {file}: Missing og:description meta tag");
        if (ogImage == null)
            warnings.Add($"[SEO] {file}: Missing og:image meta tag");
    }

    private void ValidateInternalLinks(HtmlDocument doc, string file, IReadOnlyCollection<AbsolutePath> allHtmlFiles, List<string> errors, List<string> warnings)
    {
        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links == null) return;

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(href) || href.StartsWith("http") || href.StartsWith("mailto:") || href.StartsWith("#") || href.StartsWith("data:"))
                continue;

            // Internal link - check if target exists
            var cleanHref = href.Split('#')[0].Split('?')[0];
            if (string.IsNullOrEmpty(cleanHref) || cleanHref == "/")
                continue;

            // Skip links that point to the base path root (e.g., "/StaticWGen/")
            var basePath = BasePath;
            if (!string.IsNullOrEmpty(basePath) &&
                (cleanHref == basePath || cleanHref == basePath + "/"))
                continue;

            // Decode URL-encoded characters (e.g., %20 -> space, %23 -> #)
            var decodedHref = Uri.UnescapeDataString(cleanHref).TrimStart('/');

            // Strip BasePath prefix if present (e.g., "StaticWGen/contact.html" -> "contact.html")
            var basePathTrimmed = BasePath.TrimStart('/');
            if (!string.IsNullOrEmpty(basePathTrimmed) && decodedHref.StartsWith(basePathTrimmed + "/"))
                decodedHref = decodedHref.Substring(basePathTrimmed.Length + 1);

            // Check if the target file exists on disk
            var fullPath = OutputDirectory / decodedHref;
            if (!File.Exists(fullPath))
            {
                errors.Add($"[LINK] {file}: Broken internal link: {href}");
            }
        }
    }

    private void ValidateSitemapCoverage(IReadOnlyCollection<AbsolutePath> htmlFiles, List<string> warnings)
    {
        var sitemapPath = OutputDirectory / "sitemap.xml";
        if (!sitemapPath.FileExists())
        {
            warnings.Add("[SEO] sitemap.xml not found in output directory");
            return;
        }

        var sitemapContent = sitemapPath.ReadAllText();
        var rootHtmlFiles = htmlFiles
            .Where(f => f.Parent == OutputDirectory)
            .Select(f => f.Name)
            .ToList();

        // 404 pages are intentionally excluded from sitemaps
        var excludedFromSitemap = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "404.html" };

        foreach (var fileName in rootHtmlFiles)
        {
            if (excludedFromSitemap.Contains(fileName))
                continue;

            if (!sitemapContent.Contains(fileName))
            {
                warnings.Add($"[SEO] sitemap.xml does not include: {fileName}");
            }
        }
    }

    private void ValidateRobotsTxt(List<string> warnings)
    {
        var robotsPath = OutputDirectory / "robots.txt";
        if (!robotsPath.FileExists())
        {
            warnings.Add("[SEO] robots.txt not found in output directory");
            return;
        }

        var content = robotsPath.ReadAllText();
        if (!content.Contains("Sitemap:"))
        {
            warnings.Add("[SEO] robots.txt does not reference sitemap");
        }
    }
}
