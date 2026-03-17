using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

public interface IGenerateBuildReport : IHasWebsitePaths
{
    Target GenerateBuildReport => _ => _
        .DependsOn<IGenerateWebsite>(x => x.BuildWebsite)
        .TriggeredBy<IGenerateWebsite>(x => x.BuildWebsite)
        .Executes(() =>
        {
            Information("Generating build report...");

            var htmlFiles = OutputDirectory.GlobFiles("**/*.html").ToList();
            var allFiles = OutputDirectory.GlobFiles("**/*").ToList();

            var pages = htmlFiles.Select(f =>
            {
                var info = new FileInfo(f);
                var relativePath = OutputDirectory.GetRelativePathTo(f).ToString().Replace('\\', '/');
                return new PageReport
                {
                    Output = relativePath,
                    SizeBytes = info.Length
                };
            }).ToList();

            var totalOutputSize = allFiles.Sum(f => new FileInfo(f).Length);

            var report = new BuildReport
            {
                Version = "1.0.0",
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Status = "success",
                Pages = pages,
                Summary = new BuildSummary
                {
                    TotalPages = htmlFiles.Count,
                    TotalFiles = allFiles.Count,
                    OutputSizeBytes = totalOutputSize,
                    HasSitemap = (OutputDirectory / "sitemap.xml").FileExists(),
                    HasFeed = (OutputDirectory / "feed.xml").FileExists(),
                    HasRobotsTxt = (OutputDirectory / "robots.txt").FileExists()
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(report, options);
            var reportPath = RootDirectory / "build-report.json";
            reportPath.WriteAllText(json);

            Information("Build report generated at {ReportPath}", reportPath);
            Information("  Pages: {PageCount}, Files: {FileCount}, Size: {Size}",
                report.Summary.TotalPages, report.Summary.TotalFiles,
                FormatBytes(report.Summary.OutputSizeBytes));
        });

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    record BuildReport
    {
        public required string Version { get; init; }
        public required string Timestamp { get; init; }
        public required string Status { get; init; }
        public required List<PageReport> Pages { get; init; }
        public required BuildSummary Summary { get; init; }
    }

    record PageReport
    {
        public required string Output { get; init; }
        public required long SizeBytes { get; init; }
    }

    record BuildSummary
    {
        public required int TotalPages { get; init; }
        public required int TotalFiles { get; init; }
        public required long OutputSizeBytes { get; init; }
        public required bool HasSitemap { get; init; }
        public required bool HasFeed { get; init; }
        public required bool HasRobotsTxt { get; init; }
    }
}
