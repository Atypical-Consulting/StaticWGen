using System;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

public interface ICompressOutput : IHasWebsitePaths
{
    Target CompressOutput => _ => _
        .TriggeredBy<IGenerateWebsite>(x => x.BuildWebsite)
        .Executes(() =>
        {
            try
            {
                var zipFile = RootDirectory / "site.zip";
                Log.Information($"Compressing output directory '{OutputDirectory}' to '{zipFile}'...");

                // Remove existing zip file if it exists
                if (File.Exists(zipFile))
                {
                    Log.Information("Existing zip file found. Deleting...");
                    File.Delete(zipFile);
                }

                // Compress the output directory
                OutputDirectory.ZipTo(zipFile);

                Log.Information("Output directory compressed successfully!");
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurred while compressing: {ex.Message}");
                throw;
            }
        });
}