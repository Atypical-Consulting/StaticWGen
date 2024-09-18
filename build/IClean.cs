using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

// TODO: Create IPublish interface and generate automatically the Release notes

public interface IClean : IHasWebsitePaths
{
    Target Clean => _ => _
        .Executes(() =>
        {
            Information("Cleaning output directory...");
            OutputDirectory.CreateOrCleanDirectory();
            Information("Output directory cleaned successfully!");
        });
}