using Scriban;

namespace StaticWGen.Tests;

public class TemplateRenderingTests
{
    private const string SimpleTemplate = """
        <title>{{ page_title }} - {{ site_title }}</title>
        <meta name="description" content="{{ description }}">
        <meta name="keywords" content="{{ keywords }}">
        <main>{{ content }}</main>
        """;

    [Fact]
    public void Template_RendersPageTitle()
    {
        var result = Template.Parse(SimpleTemplate).Render(new
        {
            page_title = "About",
            site_title = "My Site",
            description = "",
            keywords = "",
            content = ""
        });

        Assert.Contains("<title>About - My Site</title>", result);
    }

    [Fact]
    public void Template_RendersDescription()
    {
        var result = Template.Parse(SimpleTemplate).Render(new
        {
            page_title = "Test",
            site_title = "Site",
            description = "A great page",
            keywords = "",
            content = ""
        });

        Assert.Contains("content=\"A great page\"", result);
    }

    [Fact]
    public void Template_RendersHtmlContent()
    {
        var result = Template.Parse(SimpleTemplate).Render(new
        {
            page_title = "Test",
            site_title = "Site",
            description = "",
            keywords = "",
            content = "<h1>Hello</h1>"
        });

        Assert.Contains("<main><h1>Hello</h1></main>", result);
    }

    [Fact]
    public void Template_MenuRendering()
    {
        var menuTemplate = """
            {{ for item in menu }}
            <a href="/{{ item.url }}">{{ item.title }}</a>
            {{ end }}
            """;

        var menu = new[]
        {
            new { url = "about.html", title = "About" },
            new { url = "contact.html", title = "Contact" }
        };

        var result = Template.Parse(menuTemplate).Render(new { menu });

        Assert.Contains("href=\"/about.html\"", result);
        Assert.Contains("About</a>", result);
        Assert.Contains("href=\"/contact.html\"", result);
    }

    [Fact]
    public void Template_TagsRendering()
    {
        var tagsTemplate = """
            {{ if tags && tags.size > 0 }}
            {{ for tag in tags }}
            <a href="{{ tag.url }}">{{ tag.name }}</a>
            {{ end }}
            {{ end }}
            """;

        var tags = new[]
        {
            new { name = "csharp", url = "/tags/csharp.html" },
            new { name = "dotnet", url = "/tags/dotnet.html" }
        };

        var result = Template.Parse(tagsTemplate).Render(new { tags });

        Assert.Contains("href=\"/tags/csharp.html\"", result);
        Assert.Contains(">csharp</a>", result);
        Assert.Contains(">dotnet</a>", result);
    }

    [Fact]
    public void Template_EmptyTags_RendersNothing()
    {
        var tagsTemplate = """
            {{ if tags && tags.size > 0 }}
            <div>TAGS</div>
            {{ end }}
            """;

        var result = Template.Parse(tagsTemplate).Render(new { tags = Array.Empty<object>() });

        Assert.DoesNotContain("TAGS", result);
    }
}
