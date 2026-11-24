using System;
using System.Collections.Generic;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

namespace StaticWGen.Core;

/// <summary>
/// A static site: its settings, its folders and every generation step. <see cref="Build"/> runs the
/// whole pipeline; the other public methods are the individual steps and the content scaffolding.
/// </summary>
// ponytail: AbsolutePath/Assert come from Nuke.Utilities (no build-system dependency); swap for
// System.IO if that package ever becomes a problem for the CLI.
public partial class Site(AbsolutePath rootDirectory)
{
    public AbsolutePath RootDirectory { get; } = rootDirectory;

    // Site
    public string SiteTitle { get; set; } = "";
    public string SiteBaseUrl { get; set; } = "";
    public string Theme { get; set; } = "default";
    public string DefaultImageUrl { get; set; } = "";
    public bool IncludeDrafts { get; set; }
    public bool ContinueOnError { get; set; }

    // Analytics
    public string AnalyticsProvider { get; set; } = "";
    public string AnalyticsSiteId { get; set; } = "";
    public string AnalyticsScriptUrl { get; set; } = "";

    // Feed (title defaults to SiteTitle)
    public string FeedTitle { get => string.IsNullOrEmpty(field) ? SiteTitle : field; set; } = "";
    public string FeedDescription { get; set; } = "";
    public string FeedAuthor { get; set; } = "";

    // Output optimisation
    public int ImageMaxWidth { get; set; } = 1200;
    public int ImageQuality { get; set; } = 85;
    public bool GenerateWebP { get; set; } = true;
    public bool MinifyHtml { get; set; } = true;
    public bool FingerprintAssets { get; set; } = true;

    // Dev server
    public int Port { get; set; } = 3000;

    public AbsolutePath InputDirectory => RootDirectory / "input";
    public AbsolutePath OutputDirectory => RootDirectory / "output";
    public AbsolutePath TemplateDirectory => RootDirectory / "themes" / Theme;

    /// <summary>
    /// The path component of SiteBaseUrl ("/StaticWGen" for "https://example.github.io/StaticWGen"),
    /// "" for a root-hosted site.
    /// </summary>
    public string BasePath
    {
        get
        {
            try
            {
                var path = new Uri(SiteBaseUrl.TrimEnd('/')).AbsolutePath.TrimEnd('/');
                return path == "/" ? "" : path;
            }
            catch
            {
                return "";
            }
        }
    }

    /// <summary>A theme inherits any file or folder it does not ship (js/, css/, robots.txt) from themes/default.</summary>
    public AbsolutePath ThemePath(string relative)
    {
        var own = TemplateDirectory / relative;
        return File.Exists(own) || Directory.Exists(own) ? own : RootDirectory / "themes" / "default" / relative;
    }

    /// <summary>Validates the configuration, then generates, optimises, checks and packages the site.</summary>
    public void Build()
    {
        ValidateConfig();
        Clean();
        GenerateHtml();
        GenerateBlogIndex();
        GenerateTagPages();
        GenerateFeed();
        GenerateRobotsTxt();
        GenerateSitemap();
        CopyCss();
        CopyJsScripts();
        CopyAssets();
        Information("Static website built successfully!");
        OptimizeImages();
        OptimizeOutput();
        GenerateSearchIndex(); // before CompressOutput, so site.zip ships the search index
        Validate();
        CompressOutput();
        GenerateBuildReport();
    }

    public void ValidateConfig()
    {
        Information("Validating configuration...");
        var errors = new List<string>();

        if (string.IsNullOrEmpty(SiteTitle))
            errors.Add("SiteTitle is required: --site-title, or site_title in staticwgen.yaml (NUKE: .nuke/parameters.json, NUKE_SITE_TITLE).");

        if (string.IsNullOrEmpty(SiteBaseUrl))
            errors.Add("SiteBaseUrl is required: --site-base-url, or site_base_url in staticwgen.yaml (NUKE: .nuke/parameters.json, NUKE_SITE_BASE_URL).");
        else if (!Uri.IsWellFormedUriString(SiteBaseUrl, UriKind.Absolute))
            errors.Add($"SiteBaseUrl must be a valid absolute URL, got: {SiteBaseUrl}");

        if (!TemplateDirectory.DirectoryExists())
            errors.Add($"Theme '{Theme}' not found: expected directory {TemplateDirectory}");

        if (!string.IsNullOrEmpty(SiteBaseUrl) && SiteBaseUrl.EndsWith("/"))
            Warning("SiteBaseUrl has a trailing slash. It will be trimmed during build.");

        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Error(error);
            Assert.Fail($"Configuration validation failed with {errors.Count} error(s).");
        }

        Information("Configuration validated successfully.");
    }

    public void Clean()
    {
        Information("Cleaning output directory...");
        OutputDirectory.CreateOrCleanDirectory();
        Information("Output directory cleaned successfully!");
    }

    /// <summary>
    /// The one place that decides whether a page is published: every step asks this, so with
    /// IncludeDrafts a draft is listed (blog, tags, feed) wherever its own page links to.
    /// </summary>
    ContentStatus StatusOf(Dictionary<string, string> metadata) =>
        MarkdownService.GetContentStatus(metadata, IncludeDrafts);

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
