namespace StaticWGen.Tests;

public class YamlFrontMatterTests
{
    [Fact]
    public void ExtractMetadata_WithValidFrontMatter_ReturnsAllFields()
    {
        var content = """
            ---
            title: "My Page"
            description: "A test page"
            author: "Test Author"
            date: "2024-09-13"
            keywords: "test, unit, xunit"
            image: "./assets/test.webp"
            ---
            # Hello
            """;

        var metadata = MarkdownPipelineHelper.ExtractMetadata(content);

        Assert.Equal("My Page", metadata["title"]);
        Assert.Equal("A test page", metadata["description"]);
        Assert.Equal("Test Author", metadata["author"]);
        Assert.Equal("2024-09-13", metadata["date"]);
        Assert.Equal("test, unit, xunit", metadata["keywords"]);
        Assert.Equal("./assets/test.webp", metadata["image"]);
    }

    [Fact]
    public void ExtractMetadata_WithMissingOptionalFields_ReturnsOnlyPresentFields()
    {
        var content = """
            ---
            title: "Minimal Page"
            ---
            # Content
            """;

        var metadata = MarkdownPipelineHelper.ExtractMetadata(content);

        Assert.Single(metadata);
        Assert.Equal("Minimal Page", metadata["title"]);
        Assert.False(metadata.ContainsKey("description"));
        Assert.False(metadata.ContainsKey("author"));
    }

    [Fact]
    public void ExtractMetadata_WithNoFrontMatter_ReturnsEmptyDictionary()
    {
        var content = "# Just a heading\n\nSome content.";

        var metadata = MarkdownPipelineHelper.ExtractMetadata(content);

        Assert.Empty(metadata);
    }

    [Fact]
    public void ExtractMetadata_IsCaseInsensitive()
    {
        var content = """
            ---
            Title: "Case Test"
            DESCRIPTION: "Testing case"
            ---
            # Content
            """;

        var metadata = MarkdownPipelineHelper.ExtractMetadata(content);

        Assert.Equal("Case Test", metadata["title"]);
        Assert.Equal("Testing case", metadata["description"]);
    }

    [Fact]
    public void RemoveFrontMatter_RemovesYamlBlock()
    {
        var content = """
            ---
            title: "Test"
            ---
            # Heading

            Content here.
            """;

        var result = MarkdownPipelineHelper.RemoveFrontMatter(content);

        Assert.StartsWith("# Heading", result);
        Assert.DoesNotContain("---", result);
        Assert.DoesNotContain("title:", result);
    }

    [Fact]
    public void RemoveFrontMatter_WithNoFrontMatter_ReturnsOriginal()
    {
        var content = "# Just content\n\nNo front matter here.";

        var result = MarkdownPipelineHelper.RemoveFrontMatter(content);

        Assert.Equal(content, result);
    }

    [Fact]
    public void ExtractMetadata_WithEmptyFrontMatter_ReturnsEmptyDictionary()
    {
        var content = """
            ---
            ---
            # Content
            """;

        var metadata = MarkdownPipelineHelper.ExtractMetadata(content);

        Assert.Empty(metadata);
    }

    [Fact]
    public void ExtractMetadata_WithYamlSequence_JoinsAsCommaSeparated()
    {
        var content = """
            ---
            title: "Test"
            tags:
              - csharp
              - algorithms
              - tutorial
            ---
            # Content
            """;

        var metadata = MarkdownPipelineHelper.ExtractMetadata(content);

        Assert.Equal("Test", metadata["title"]);
        Assert.Equal("csharp, algorithms, tutorial", metadata["tags"]);
    }

    [Fact]
    public void ExtractMetadata_WithInlineYamlSequence_JoinsAsCommaSeparated()
    {
        var content = """
            ---
            title: "Test"
            tags: [a, b, c]
            ---
            # Content
            """;

        var metadata = MarkdownPipelineHelper.ExtractMetadata(content);

        Assert.Equal("a, b, c", metadata["tags"]);
    }

    [Fact]
    public void ExtractMetadata_WithNestedObject_SkipsGracefully()
    {
        var content = """
            ---
            title: "Test"
            nested:
              key: value
            ---
            # Content
            """;

        var metadata = MarkdownPipelineHelper.ExtractMetadata(content);

        Assert.Equal("Test", metadata["title"]);
        Assert.False(metadata.ContainsKey("nested"));
    }

    [Fact]
    public void ExtractMetadata_WithNullValues_HandlesGracefully()
    {
        var content = """
            ---
            title: "Test"
            description:
            ---
            # Content
            """;

        var metadata = MarkdownPipelineHelper.ExtractMetadata(content);

        Assert.Equal("Test", metadata["title"]);
        Assert.Equal("", metadata["description"]);
    }
}
