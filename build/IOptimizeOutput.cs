using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Nuke.Common;
using Nuke.Common.IO;
using WebMarkupMin.Core;
using static Serilog.Log;

public interface IOptimizeOutput : IHasWebsitePaths
{
    [Parameter("Enable HTML minification")]
    bool MinifyHtml => TryGetValue<bool?>(() => MinifyHtml) ?? true;

    [Parameter("Enable asset fingerprinting with content hash")]
    bool FingerprintAssets => TryGetValue<bool?>(() => FingerprintAssets) ?? true;

    Target OptimizeOutput => _ => _
        .DependsOn<IGenerateWebsite>(x => x.BuildWebsite)
        .TriggeredBy<IGenerateWebsite>(x => x.BuildWebsite)
        .Before<IValidateOutput>(x => x.Validate)
        .Before<ICompressOutput>(x => x.CompressOutput)
        .Executes(() =>
        {
            Information("Optimizing output...");

            var assetMap = new Dictionary<string, string>();

            if (FingerprintAssets)
                assetMap = FingerprintStaticAssets();

            if (MinifyHtml)
                MinifyHtmlFiles(assetMap);
            else if (assetMap.Count > 0)
                UpdateAssetReferences(assetMap);

            if (assetMap.Count > 0)
                Information("Fingerprinted {Count} assets", assetMap.Count);
        });

    private Dictionary<string, string> FingerprintStaticAssets()
    {
        var assetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var extensions = new[] { ".css", ".js" };

        var assetFiles = OutputDirectory.GlobFiles("**/*")
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        foreach (var file in assetFiles)
        {
            var content = File.ReadAllBytes(file);
            var hash = Convert.ToHexString(SHA256.HashData(content)).Substring(0, 8).ToLowerInvariant();

            var dir = Path.GetDirectoryName(file.ToString())!;
            var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
            var ext = Path.GetExtension(file);
            var newName = $"{nameWithoutExt}.{hash}{ext}";
            var newPath = Path.Combine(dir, newName);

            File.Copy(file, newPath, true);
            File.Delete(file);

            var relOriginal = OutputDirectory.GetRelativePathTo(file).ToString().Replace('\\', '/');
            var relNew = OutputDirectory.GetRelativePathTo((AbsolutePath)newPath).ToString().Replace('\\', '/');

            assetMap[relOriginal] = relNew;
            Information("  {Original} → {New}", relOriginal, relNew);
        }

        return assetMap;
    }

    private void MinifyHtmlFiles(Dictionary<string, string> assetMap)
    {
        var htmlFiles = OutputDirectory.GlobFiles("**/*.html").ToList();
        var minifier = new HtmlMinifier(new HtmlMinificationSettings
        {
            RemoveHtmlComments = true,
            RemoveOptionalEndTags = false,
            WhitespaceMinificationMode = WhitespaceMinificationMode.Medium,
            RemoveRedundantAttributes = true,
            MinifyEmbeddedCssCode = true,
            MinifyEmbeddedJsCode = true,
            MinifyInlineCssCode = true,
            MinifyInlineJsCode = true
        });

        long totalOriginal = 0;
        long totalMinified = 0;

        foreach (var file in htmlFiles)
        {
            var content = file.ReadAllText();
            var originalSize = content.Length;

            // Update asset references if fingerprinting is enabled
            if (assetMap.Count > 0)
            {
                foreach (var (original, fingerprinted) in assetMap)
                {
                    content = content.Replace(original, fingerprinted);
                    // Also match without leading path for relative refs like "css/prism.css"
                    var origFileName = Path.GetFileName(original);
                    var newFileName = Path.GetFileName(fingerprinted);
                    content = content.Replace(origFileName, newFileName);
                }
            }

            var result = minifier.Minify(content);
            if (result.Errors.Count == 0)
            {
                file.WriteAllText(result.MinifiedContent);
                totalOriginal += originalSize;
                totalMinified += result.MinifiedContent.Length;
            }
            else
            {
                // Write with asset references updated but not minified
                file.WriteAllText(content);
                foreach (var error in result.Errors)
                    Warning("Minification warning in {File}: {Message}", file.Name, error.Message);
            }
        }

        if (totalOriginal > 0)
        {
            var reduction = (1.0 - (double)totalMinified / totalOriginal) * 100;
            Information("HTML minified: {Original} → {Minified} ({Reduction:F1}% reduction)",
                FormatBytes(totalOriginal), FormatBytes(totalMinified), reduction);
        }
    }

    private void UpdateAssetReferences(Dictionary<string, string> assetMap)
    {
        var htmlFiles = OutputDirectory.GlobFiles("**/*.html").ToList();

        foreach (var file in htmlFiles)
        {
            var content = file.ReadAllText();
            foreach (var (original, fingerprinted) in assetMap)
            {
                var origFileName = Path.GetFileName(original);
                var newFileName = Path.GetFileName(fingerprinted);
                content = content.Replace(origFileName, newFileName);
            }
            file.WriteAllText(content);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
