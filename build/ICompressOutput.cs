using System;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

public interface ICompressOutput : IHasWebsitePaths
{
    Target CompressOutput => _ => _
        .TriggeredBy<IGenerateWebsite>(x => x.BuildWebsite)
        .Executes(() =>
        {
            try
            {
                var zipFile = RootDirectory / "site.zip";
                Information("Compressing output directory \'{OutputDirectory}\' to \'{ZipFile}\'...", OutputDirectory, zipFile);

                // Remove existing zip file if it exists
                if (zipFile.Exists())
                {
                    Information("Existing zip file found. Deleting...");
                    zipFile.DeleteFile();
                }

                // Compress the output directory
                OutputDirectory.ZipTo(zipFile);

                Information("Output directory compressed successfully!");
            }
            catch (Exception ex)
            {
                Error("An error occurred while compressing: {ExMessage}", ex.Message);
                throw;
            }
        });
}