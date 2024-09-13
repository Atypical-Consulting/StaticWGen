using Nuke.Common;
using Nuke.Common.IO;

public interface IHasWebsitePaths : INukeBuild
{
    [Parameter("Base URL of the site")][Required]
    string SiteBaseUrl => TryGetValue(() => SiteBaseUrl);
    
    AbsolutePath InputDirectory => RootDirectory / "input";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath TemplateDirectory => RootDirectory / "template";
}