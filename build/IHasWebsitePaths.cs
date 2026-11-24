using System;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;

public interface IHasWebsitePaths : INukeBuild
{
    [Parameter("Title of the site")][Required]
    string SiteTitle => TryGetValue(() => SiteTitle);

    [Parameter("Base URL of the site")][Required]
    string SiteBaseUrl => TryGetValue(() => SiteBaseUrl);

    /// <summary>
    /// Extracts the path component from SiteBaseUrl (e.g., "/StaticWGen" from
    /// "https://example.github.io/StaticWGen"). Returns "" for root-hosted sites.
    /// </summary>
    string BasePath
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

    [Parameter("Theme name (directory under themes/)")]
    string Theme => TryGetValue(() => Theme) ?? "default";

    AbsolutePath InputDirectory => RootDirectory / "input";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    AbsolutePath TemplateDirectory => RootDirectory / "themes" / Theme;
}
