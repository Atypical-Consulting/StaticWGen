using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HtmlAgilityPack;
using Nuke.Common;
using Nuke.Common.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using static Serilog.Log;

public interface IOptimizeImages : IHasWebsitePaths
{
    [Parameter("Maximum image width in pixels")]
    int ImageMaxWidth => TryGetValue<int?>(() => ImageMaxWidth) ?? 1200;

    [Parameter("Image compression quality (1-100)")]
    int ImageQuality => TryGetValue<int?>(() => ImageQuality) ?? 85;

    [Parameter("Generate WebP variants of images")]
    bool GenerateWebP => TryGetValue<bool?>(() => GenerateWebP) ?? true;

    Target OptimizeImages => _ => _
        .DependsOn<IGenerateWebsite>(x => x.CopyAssets)
        .TriggeredBy<IGenerateWebsite>(x => x.CopyAssets)
        .Before<IOptimizeOutput>(x => x.OptimizeOutput)
        .Executes(() =>
        {
            Information("Optimizing images...");

            var assetsDir = OutputDirectory / "assets";
            if (!assetsDir.DirectoryExists())
            {
                Information("No assets directory found. Skipping image optimization.");
                return;
            }

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var imageFiles = assetsDir.GlobFiles("**/*")
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            if (imageFiles.Count == 0)
            {
                Information("No images found in assets. Skipping.");
                return;
            }

            long originalSize = 0;
            long optimizedSize = 0;
            var webpGenerated = 0;

            foreach (var file in imageFiles)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                originalSize += new FileInfo(file).Length;

                try
                {
                    using var image = Image.Load(file);
                    var resized = false;

                    // Resize if wider than max width
                    if (image.Width > ImageMaxWidth)
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = new Size(ImageMaxWidth, 0)
                        }));
                        resized = true;
                    }

                    // Save optimized original
                    if (resized)
                    {
                        image.Save(file);
                        Information("  Resized: {File} to {Width}px", file.Name, image.Width);
                    }

                    // Generate WebP variant
                    if (GenerateWebP && ext != ".webp")
                    {
                        var webpPath = Path.ChangeExtension(file, ".webp");
                        if (!File.Exists(webpPath))
                        {
                            image.Save(webpPath, new WebpEncoder { Quality = ImageQuality });
                            webpGenerated++;
                        }
                    }

                    optimizedSize += new FileInfo(file).Length;
                }
                catch (Exception ex)
                {
                    Warning("Could not optimize {File}: {Message}", file.Name, ex.Message);
                    optimizedSize += new FileInfo(file).Length;
                }
            }

            // Post-process HTML to add lazy loading and dimensions
            AddImageAttributes();

            if (originalSize > 0)
            {
                var reduction = (1.0 - (double)optimizedSize / originalSize) * 100;
                Information("Images optimized: {Count} files, {WebP} WebP generated", imageFiles.Count, webpGenerated);
                if (reduction > 0)
                    Information("  Size: {Original} → {Optimized} ({Reduction:F1}% reduction)",
                        FormatBytes(originalSize), FormatBytes(optimizedSize), reduction);
            }
        });

    private void AddImageAttributes()
    {
        var htmlFiles = OutputDirectory.GlobFiles("**/*.html").ToList();

        foreach (var file in htmlFiles)
        {
            var content = file.ReadAllText();
            var doc = new HtmlDocument();
            doc.LoadHtml(content);
            var modified = false;

            var images = doc.DocumentNode.SelectNodes("//article//img[@src]");
            if (images == null) continue;

            foreach (var img in images)
            {
                // Add loading="lazy"
                if (img.GetAttributeValue("loading", null) == null)
                {
                    img.SetAttributeValue("loading", "lazy");
                    modified = true;
                }

                // Add decoding="async"
                if (img.GetAttributeValue("decoding", null) == null)
                {
                    img.SetAttributeValue("decoding", "async");
                    modified = true;
                }

                // Try to read image dimensions
                var src = img.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(src) &&
                    img.GetAttributeValue("width", null) == null)
                {
                    var imgPath = src.StartsWith("/")
                        ? OutputDirectory / src.TrimStart('/')
                        : OutputDirectory / src;

                    if (imgPath.FileExists())
                    {
                        try
                        {
                            var info = Image.Identify(imgPath);
                            if (info != null)
                            {
                                img.SetAttributeValue("width", info.Width.ToString());
                                img.SetAttributeValue("height", info.Height.ToString());
                                modified = true;
                            }
                        }
                        catch { /* skip if can't read dimensions */ }
                    }
                }
            }

            if (modified)
            {
                using var writer = new StringWriter();
                doc.Save(writer);
                file.WriteAllText(writer.ToString());
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
