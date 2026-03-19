using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

public interface IGenerateSearchIndex : IHasWebsitePaths
{
    Target GenerateSearchIndex => _ => _
        .DependsOn<IGenerateWebsite>(x => x.GenerateHtml)
        .TriggeredBy<IGenerateWebsite>(x => x.GenerateHtml)
        .Executes(() =>
        {
            Information("Generating search index...");

            var htmlFiles = OutputDirectory.GlobFiles("*.html")
                .Where(f => f.Name != "404.html")
                .ToList();

            var entries = new List<SearchEntry>();

            foreach (var file in htmlFiles)
            {
                var content = file.ReadAllText();
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                // Extract title
                var titleNode = doc.DocumentNode.SelectSingleNode("//title");
                var title = titleNode?.InnerText?.Split(" - ").FirstOrDefault()?.Trim() ?? file.NameWithoutExtension;

                // Extract description
                var descNode = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
                var description = descNode?.GetAttributeValue("content", "") ?? "";

                // Extract tags
                var tagNodes = doc.DocumentNode.SelectNodes("//meta[@property='article:tag']");
                var tags = tagNodes?.Select(n => n.GetAttributeValue("content", "")).ToList() ?? new List<string>();

                // Extract text content from article
                var articleNode = doc.DocumentNode.SelectSingleNode("//article") ??
                                  doc.DocumentNode.SelectSingleNode("//main");
                var textContent = "";
                if (articleNode != null)
                {
                    textContent = articleNode.InnerText;
                    // Clean up whitespace
                    textContent = Regex.Replace(textContent, @"\s+", " ").Trim();
                    // Truncate to keep index size reasonable
                    if (textContent.Length > 500)
                        textContent = textContent.Substring(0, 500);
                }

                entries.Add(new SearchEntry
                {
                    Url = $"{BasePath}/{file.Name}",
                    Title = title,
                    Description = description,
                    Tags = tags,
                    Content = textContent
                });
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(entries, options);
            var indexPath = OutputDirectory / "search-index.json";
            indexPath.WriteAllText(json);

            Information("Search index generated at {Path} with {Count} entries ({Size} bytes)",
                indexPath, entries.Count, json.Length);
        });

    record SearchEntry
    {
        public required string Url { get; init; }
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required List<string> Tags { get; init; }
        public required string Content { get; init; }
    }
}
