using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using static Serilog.Log;

public interface ISitemap : IHasWebsitePaths
{
    Target GenerateSitemap => _ => _
        .DependsOn<IGenerateWebsite>(x => x.GenerateHtml)
        .TriggeredBy<IGenerateWebsite>(x => x.GenerateHtml)
        .Executes(() =>
        {
            Information("Generating sitemap.xml...");

            var baseUrl = SiteBaseUrl.TrimEnd('/'); // You need to define SiteBaseUrl

            var urls = OutputDirectory.GlobFiles("**/*.html")
                .Where(file => file.Name != "404.html") // Exclude error pages from sitemap
                .Select(file =>
                {
                    var relativePath = OutputDirectory.GetRelativePathTo(file).ToString().Replace('\\', '/');
                    return new Uri(new Uri(baseUrl + "/"), relativePath);
                });

            var sitemap = new XDocument(
                new XElement("urlset",
                    new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                    new XAttribute(XNamespace.Xmlns + "xhtml", "http://www.w3.org/1999/xhtml"),
                    new XAttribute(XNamespace.Xmlns + "schemaLocation", "http://www.sitemaps.org/schemas/sitemap/0.9"),
                    from url in urls
                    select new XElement("url",
                        new XElement("loc", url.AbsoluteUri)
                    )
                )
            );

            var sitemapPath = OutputDirectory / "sitemap.xml";
            sitemap.Save(sitemapPath);

            Information("sitemap.xml generated at {SitemapPath}", sitemapPath);
        });
}