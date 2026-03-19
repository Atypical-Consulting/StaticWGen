using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

public interface IValidateConfig : IHasWebsitePaths
{
    Target ValidateConfig => _ => _
        .Executes(() =>
        {
            Information("Validating configuration...");

            // Load .env file if present
            LoadDotEnv();

            var errors = new List<string>();

            // Validate required parameters
            if (string.IsNullOrEmpty(SiteTitle))
                errors.Add("SiteTitle is required. Set via --site-title, parameters.json, or NUKE_SITE_TITLE env var.");

            if (string.IsNullOrEmpty(SiteBaseUrl))
                errors.Add("SiteBaseUrl is required. Set via --site-base-url, parameters.json, or NUKE_SITE_BASE_URL env var.");
            else if (!Uri.IsWellFormedUriString(SiteBaseUrl, UriKind.Absolute))
                errors.Add($"SiteBaseUrl must be a valid absolute URL, got: {SiteBaseUrl}");

            // Validate optional parameters
            if (!string.IsNullOrEmpty(SiteBaseUrl) && SiteBaseUrl.EndsWith("/"))
                Warning("SiteBaseUrl has a trailing slash. It will be trimmed during build.");

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                    Error(error);

                Information("");
                Information("Configuration Reference:");
                Information("  SiteTitle       (required)  Site title for <title> and navigation");
                Information("  SiteBaseUrl     (required)  Base URL for sitemap and canonical links");
                Information("  DefaultImageUrl (optional)  Default Open Graph image");
                Information("  AnalyticsProvider (optional)  plausible, google, custom");
                Information("  AnalyticsSiteId (optional)  Domain or measurement ID");
                Information("  ImageName       (optional)  Docker image name");
                Information("  HostPort        (optional)  Docker host port");
                Information("");
                Information("Set via: CLI flags, .nuke/parameters.json, .env file, or NUKE_* env vars.");

                Assert.Fail($"Configuration validation failed with {errors.Count} error(s).");
            }

            Information("Configuration validated successfully.");
        });

    private static void LoadDotEnv()
    {
        var envFile = NukeBuild.RootDirectory / ".env";
        if (!envFile.FileExists())
            return;

        Information("Loading .env file...");
        var lines = File.ReadAllLines(envFile);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = trimmed.Substring(0, eqIndex).Trim();
            var value = trimmed.Substring(eqIndex + 1).Trim();

            // Remove surrounding quotes
            if (value.Length >= 2 &&
                ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                 (value.StartsWith("'") && value.EndsWith("'"))))
            {
                value = value.Substring(1, value.Length - 2);
            }

            // Only set if not already set (env vars take precedence over .env)
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
                Verbose("Set env var from .env: {Key}", key);
            }
        }
    }
}
