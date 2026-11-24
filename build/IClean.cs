using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

public interface IClean : IHasWebsitePaths
{
    Target Clean => _ => _
        .DependsOn<IValidateConfig>(x => x.ValidateConfig)
        .Executes(() =>
        {
            Information("Cleaning output directory...");
            OutputDirectory.CreateOrCleanDirectory();
            Information("Output directory cleaned successfully!");
        });
}