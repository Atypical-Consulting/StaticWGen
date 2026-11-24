using Nuke.Common.IO;
using StaticWGen.Core;

namespace StaticWGen.Tests;

public class SiteBuildTests
{
    [Fact]
    public void Build_GeneratesTheSampleSite_OutsideTheRepository()
    {
        var repo = (AbsolutePath)AppContext.BaseDirectory;
        while (!(repo / "StaticWGen.sln").FileExists())
            repo = repo.Parent;

        var root = (AbsolutePath)Path.Combine(Path.GetTempPath(), $"staticwgen-{Guid.NewGuid():N}");
        try
        {
            (repo / "input").Copy(root / "input");
            (repo / "themes").Copy(root / "themes");

            new Site(root) { SiteTitle = "Test", SiteBaseUrl = "https://example.org/sub" }.Build();

            foreach (var file in new[] { "index.html", "blog.html", "tags.html", "feed.xml", "sitemap.xml", "robots.txt", "search-index.json" })
                Assert.True((root / "output" / file).FileExists(), $"missing output/{file}");
            Assert.Contains("https://example.org/sub/about.html", (root / "output" / "sitemap.xml").ReadAllText());
            Assert.True((root / "site.zip").FileExists());
        }
        finally
        {
            root.DeleteDirectory();
        }
    }
}
