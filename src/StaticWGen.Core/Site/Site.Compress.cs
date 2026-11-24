using System;
using System.IO.Compression;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

namespace StaticWGen.Core;

public partial class Site
{
    public void CompressOutput()
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
            ZipFile.CreateFromDirectory(OutputDirectory, zipFile);

            Information("Output directory compressed successfully!");
        }
        catch (Exception ex)
        {
            Error("An error occurred while compressing: {ExMessage}", ex.Message);
            throw;
        }
    }
}
