using System.IO;
using Nuke.Common;
using Nuke.Common.IO;

public interface IHasWebsitePaths : INukeBuild
{
    [Parameter("Base URL of the site")][Required]
    string SiteBaseUrl => TryGetValue(() => SiteBaseUrl);

    [Parameter("Theme name (directory under themes/ or template/ for legacy)")]
    string Theme => TryGetValue(() => Theme) ?? "";

    AbsolutePath InputDirectory => RootDirectory / "input";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    /// <summary>
    /// Resolves the template directory based on Theme parameter:
    /// 1. If Theme is set and themes/{Theme}/ exists, use it
    /// 2. Otherwise fall back to template/ (legacy)
    /// </summary>
    AbsolutePath TemplateDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(Theme))
            {
                var themeDir = RootDirectory / "themes" / Theme;
                if (Directory.Exists(themeDir))
                    return themeDir;
            }

            return RootDirectory / "template";
        }
    }
}
