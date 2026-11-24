using System;
using System.IO;
using Nuke.Common;
using Nuke.Common.Tools.Docker;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Serilog.Log;

/// <summary>
/// Thin Nuke front-end over StaticWGen.Core: parameters map onto <see cref="Site"/>, targets call it.
/// </summary>
class Build : NukeBuild
{
    public static int Main()
    {
        LoadDotEnv(); // before Nuke reads the parameters below
        return Execute<Build>(x => x.DeployDockerImage);
    }

    [Parameter("Title of the site")] readonly string SiteTitle = "";
    [Parameter("Base URL of the site")] readonly string SiteBaseUrl = "";
    [Parameter("Theme name (directory under themes/)")] readonly string Theme = "default";
    [Parameter("Default image URL for social sharing")] readonly string DefaultImageUrl = "";
    [Parameter("Analytics provider: plausible, google, custom, or empty to disable")] readonly string AnalyticsProvider = "";
    [Parameter("Analytics site ID (domain for Plausible, measurement ID for GA4)")] readonly string AnalyticsSiteId = "";
    [Parameter("Custom analytics script URL")] readonly string AnalyticsScriptUrl = "";
    [Parameter("Include draft pages in output")] readonly bool IncludeDrafts;
    [Parameter("Continue processing remaining files when one fails")] readonly bool ContinueOnError;
    [Parameter("Title for the Atom feed (defaults to SiteTitle)")] readonly string FeedTitle;
    [Parameter("Description for the Atom feed")] readonly string FeedDescription = "";
    [Parameter("Author name for the Atom feed")] readonly string FeedAuthor = "";
    [Parameter("Maximum image width in pixels")] readonly int ImageMaxWidth = 1200;
    [Parameter("Image compression quality (1-100)")] readonly int ImageQuality = 85;
    [Parameter("Generate WebP variants of images")] readonly bool GenerateWebP = true;
    [Parameter("Enable HTML minification")] readonly bool MinifyHtml = true;
    [Parameter("Enable asset fingerprinting with content hash")] readonly bool FingerprintAssets = true;
    [Parameter("Port for the development server")] readonly int Port = 3000;

    [Parameter("Title for the new content")] readonly string Title = "";
    [Parameter("Tags for the new post (comma-separated)")] readonly string Tags = "";
    [Parameter("Author name")] readonly string Author = "";

    [Parameter("Docker image name to build")] readonly string ImageName;
    [Parameter("Docker image version tag")] readonly string VersionTag;
    [Parameter("Docker container name")] readonly string ContainerName;
    [Parameter("Host port to publish the container")] readonly int HostPort;
    [Parameter("Container port to expose the application")] readonly int ContainerPort;

    Site Site => new(RootDirectory)
    {
        SiteTitle = SiteTitle,
        SiteBaseUrl = SiteBaseUrl,
        Theme = Theme,
        DefaultImageUrl = DefaultImageUrl,
        AnalyticsProvider = AnalyticsProvider,
        AnalyticsSiteId = AnalyticsSiteId,
        AnalyticsScriptUrl = AnalyticsScriptUrl,
        IncludeDrafts = IncludeDrafts,
        ContinueOnError = ContinueOnError,
        FeedTitle = FeedTitle,
        FeedDescription = FeedDescription,
        FeedAuthor = FeedAuthor,
        ImageMaxWidth = ImageMaxWidth,
        ImageQuality = ImageQuality,
        GenerateWebP = GenerateWebP,
        MinifyHtml = MinifyHtml,
        FingerprintAssets = FingerprintAssets,
        Port = Port,
    };

    Target BuildWebsite => _ => _.Executes(() => Site.Build());

    Target Watch => _ => _.Executes(() =>
    {
        var site = Site;
        site.Build();
        site.Watch();
    });

    Target NewPost => _ => _.Executes(() => Site.NewPost(Title, Tags, Author));
    Target NewPage => _ => _.Executes(() => Site.NewPage(Title, Tags, Author));
    Target Init => _ => _.Executes(() => Site.Init(Title, Author));

    Target BuildDockerImage => _ => _
        .DependsOn(BuildWebsite)
        .Requires(() => ImageName, () => VersionTag)
        .Executes(() =>
        {
            DockerBuild(x => x
                .SetPath(RootDirectory)
                .SetTag($"{ImageName}:{VersionTag}"));
            Information("Docker image {ImageName}:{VersionTag} built successfully!", ImageName, VersionTag);
        });

    Target DeployDockerImage => _ => _
        .DependsOn(BuildDockerImage)
        .Requires(() => ContainerName)
        .Executes(() =>
        {
            if (DockerPs(x => x.SetFormat("{{.Names}}").SetFilter($"name={ContainerName}")).Count != 0)
            {
                Information("Stopping and removing existing container {ContainerName}...", ContainerName);
                DockerStop(x => x.SetContainers(ContainerName));
                DockerRm(x => x.SetForce(true).SetContainers(ContainerName));
            }

            DockerRun(x => x
                .SetDetach(true)
                .SetPublish($"{HostPort}:{ContainerPort}")
                .SetName(ContainerName)
                .SetImage($"{ImageName}:{VersionTag}"));
            Information("Docker container {ContainerName} running at http://localhost:{HostPort}", ContainerName, HostPort);
        });

    /// <summary>Loads KEY=VALUE pairs from .env into the environment (existing variables win).</summary>
    static void LoadDotEnv()
    {
        var envFile = RootDirectory / ".env";
        if (!File.Exists(envFile))
            return;

        foreach (var line in File.ReadAllLines(envFile))
        {
            var trimmed = line.Trim();
            var eq = trimmed.IndexOf('=');
            if (trimmed.StartsWith('#') || eq <= 0)
                continue;

            var key = trimmed[..eq].Trim();
            var value = trimmed[(eq + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                value = value[1..^1];

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
