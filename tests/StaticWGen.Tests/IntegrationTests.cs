using System.Xml.Linq;
using Scriban;

namespace StaticWGen.Tests;

public class IntegrationTests
{
    [Fact]
    public void EndToEnd_MarkdownWithFrontMatter_ProducesCompleteHtml()
    {
        var markdown = """
            ---
            title: "Integration Test"
            description: "Testing end-to-end"
            author: "Tester"
            date: "2024-09-15"
            keywords: "test, integration"
            ---
            # Welcome

            This is a test page with **bold** and *italic* text.

            - Item one
            - Item two

            ```csharp
            Console.WriteLine("Hello!");
            ```
            """;

        var template = """
            <!doctype html>
            <html>
            <head><title>{{ page_title }} - {{ site_title }}</title></head>
            <body>
            <main>{{ content }}</main>
            </body>
            </html>
            """;

        // Extract metadata
        var metadata = MarkdownPipelineHelper.ExtractMetadata(markdown);
        Assert.Equal("Integration Test", metadata["title"]);

        // Remove front matter and convert
        var content = MarkdownPipelineHelper.RemoveFrontMatter(markdown);
        var html = MarkdownPipelineHelper.ConvertToHtmlWithMermaidRenderer(content);

        // Render template
        var finalHtml = Template.Parse(template).Render(new
        {
            page_title = metadata["title"],
            site_title = "Test Site",
            content = html
        });

        // Verify complete output
        Assert.Contains("<!doctype html>", finalHtml);
        Assert.Contains("<title>Integration Test - Test Site</title>", finalHtml);
        Assert.Contains("<h1", finalHtml);
        Assert.Contains("Welcome", finalHtml);
        Assert.Contains("<strong>bold</strong>", finalHtml);
        Assert.Contains("<em>italic</em>", finalHtml);
        Assert.Contains("<li>Item one</li>", finalHtml);
        Assert.Contains("class=\"language-csharp\"", finalHtml);
    }

    [Fact]
    public void EndToEnd_AllExtensionsTogether_RenderCorrectly()
    {
        var markdown = """
            Hello :wave:

            Math: \( E = mc^2 \)

            ```csharp
            var x = 1;
            ```

            ```mermaid
            graph TD;
                A-->B;
            ```
            """;

        var html = MarkdownPipelineHelper.ConvertToHtmlWithMermaidRenderer(markdown);

        // Emoji
        Assert.Contains("👋", html);
        // Code block with Prism class
        Assert.Contains("class=\"language-csharp\"", html);
        // Mermaid block with mermaid class
        Assert.Contains("<pre class=\"mermaid\">", html);
        Assert.DoesNotContain("language-mermaid", html);
    }

    [Fact]
    public void SitemapXml_ValidStructure()
    {
        var baseUrl = "https://example.com";
        var pages = new[] { "index.html", "about.html", "contact.html" };

        var sitemap = new XDocument(
            new XElement("urlset",
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                from page in pages
                select new XElement("url",
                    new XElement("loc", $"{baseUrl}/{page}")
                )
            )
        );

        var xml = sitemap.ToString();

        Assert.Contains("<urlset", xml);
        Assert.Contains($"<loc>{baseUrl}/index.html</loc>", xml);
        Assert.Contains($"<loc>{baseUrl}/about.html</loc>", xml);
        Assert.Contains($"<loc>{baseUrl}/contact.html</loc>", xml);
        Assert.Equal(3, sitemap.Descendants("url").Count());
    }

    [Fact]
    public void RobotsTxt_CorrectFormat()
    {
        var robotsTemplate = "User-agent: *\nAllow: /\nSitemap: {{ site_base_url }}/sitemap.xml";
        var result = Template.Parse(robotsTemplate).Render(new { site_base_url = "https://example.com" });

        Assert.Contains("User-agent: *", result);
        Assert.Contains("Allow: /", result);
        Assert.Contains("Sitemap: https://example.com/sitemap.xml", result);
    }
}
