using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

public interface IGenerateFeed : IHasWebsitePaths
{
    [Parameter("Title of the site")][Required]
    string SiteTitle => TryGetValue(() => SiteTitle);

    [Parameter("Title for the Atom feed (defaults to SiteTitle)")]
    string FeedTitle => TryGetValue(() => FeedTitle) ?? SiteTitle;

    [Parameter("Description for the Atom feed")]
    string FeedDescription => TryGetValue(() => FeedDescription) ?? "";

    [Parameter("Author name for the Atom feed")]
    string FeedAuthor => TryGetValue(() => FeedAuthor) ?? "";

    Target GenerateFeed => _ => _
        .DependsOn<IGenerateWebsite>(x => x.GenerateHtml)
        .TriggeredBy<IGenerateWebsite>(x => x.GenerateHtml)
        .Executes(() =>
        {
            Information("Generating Atom feed...");

            var baseUrl = SiteBaseUrl.TrimEnd('/');
            var entries = CollectFeedEntries(baseUrl);

            if (entries.Count == 0)
            {
                Warning("No pages with date metadata found. Skipping feed generation.");
                return;
            }

            var feedXml = BuildAtomFeed(baseUrl, entries);

            var feedPath = OutputDirectory / "feed.xml";
            feedXml.Save(feedPath);

            Information("feed.xml generated at {FeedPath} with {Count} entries", feedPath, entries.Count);
        });

    private List<FeedEntry> CollectFeedEntries(string baseUrl)
    {
        var markdownFiles = InputDirectory.GlobFiles("**/*.md");
        var entries = new List<FeedEntry>();

        foreach (var file in markdownFiles)
        {
            var (metadata, _) = MarkdownHelper.ParseMarkdownFile(file);

            if (!metadata.TryGetValue("date", out var dateStr))
                continue;

            if (!DateTime.TryParse(dateStr, out var date))
            {
                Warning("Could not parse date '{Date}' in {File}", dateStr, file);
                continue;
            }

            entries.Add(new FeedEntry
            {
                Title = metadata.TryGetValue("title", out var title) ? title : file.NameWithoutExtension,
                Link = $"{baseUrl}/{file.NameWithoutExtension}.html",
                Summary = metadata.TryGetValue("description", out var desc) ? desc : "",
                Author = metadata.TryGetValue("author", out var author) ? author : FeedAuthor,
                Published = date
            });
        }

        return entries
            .OrderByDescending(e => e.Published)
            .Take(20)
            .ToList();
    }

    private XDocument BuildAtomFeed(string baseUrl, List<FeedEntry> entries)
    {
        XNamespace atom = "http://www.w3.org/2005/Atom";

        var feedElements = new List<object>
        {
            new XElement(atom + "title", FeedTitle),
            new XElement(atom + "link",
                new XAttribute("href", $"{baseUrl}/feed.xml"),
                new XAttribute("rel", "self")),
            new XElement(atom + "link",
                new XAttribute("href", baseUrl)),
            new XElement(atom + "id", baseUrl + "/"),
            new XElement(atom + "updated", entries.First().Published.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        };

        if (!string.IsNullOrWhiteSpace(FeedDescription))
            feedElements.Add(new XElement(atom + "subtitle", FeedDescription));

        if (!string.IsNullOrWhiteSpace(FeedAuthor))
        {
            feedElements.Add(new XElement(atom + "author",
                new XElement(atom + "name", FeedAuthor)));
        }

        foreach (var entry in entries)
        {
            var entryElements = new List<object>
            {
                new XElement(atom + "title", entry.Title),
                new XElement(atom + "link",
                    new XAttribute("href", entry.Link)),
                new XElement(atom + "id", entry.Link),
                new XElement(atom + "published", entry.Published.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                new XElement(atom + "updated", entry.Published.ToString("yyyy-MM-ddTHH:mm:ssZ"))
            };

            if (!string.IsNullOrWhiteSpace(entry.Summary))
                entryElements.Add(new XElement(atom + "summary", entry.Summary));

            if (!string.IsNullOrWhiteSpace(entry.Author))
            {
                entryElements.Add(new XElement(atom + "author",
                    new XElement(atom + "name", entry.Author)));
            }

            feedElements.Add(new XElement(atom + "entry", entryElements));
        }

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(atom + "feed", feedElements));
    }

    record FeedEntry
    {
        public required string Title { get; init; }
        public required string Link { get; init; }
        public required string Summary { get; init; }
        public required string Author { get; init; }
        public required DateTime Published { get; init; }
    }
}
