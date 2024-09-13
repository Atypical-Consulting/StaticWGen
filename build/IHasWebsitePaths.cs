using Nuke.Common;
using Nuke.Common.IO;

public interface IHasWebsitePaths : INukeBuild
{
    AbsolutePath InputDirectory => RootDirectory / "input";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath TemplateDirectory => RootDirectory / "template";
}