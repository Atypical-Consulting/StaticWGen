using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

public interface IClean : IHasWebsitePaths
{
    Target Clean => _ => _
        .Executes(() =>
        {
            Log.Information("Cleaning output directory...");
            OutputDirectory.CreateOrCleanDirectory();
            Log.Information("Output directory cleaned successfully!");
        });
}