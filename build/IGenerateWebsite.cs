using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;
using Scriban;
using Serilog;
using YamlDotNet.RepresentationModel;

public interface IGenerateWebsite : IHasWebsitePaths
{
    [Parameter("Title of the site")][Required]
    string SiteTitle => TryGetValue(() => SiteTitle);
    
    [Parameter("Default image URL for social sharing")]
    string DefaultImageUrl => TryGetValue(() => DefaultImageUrl) ?? "";
    
    Target GenerateHtml => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            Log.Information("Generating HTML files from Markdown...");
            
            var template = TemplateDirectory / "template.html";
            var templateContent = template.ReadAllText();
            
            // Use advanced extensions for Markdown processing
            var pipeline = new MarkdownPipelineBuilder()
                .UseYamlFrontMatter()
                .UseEmojiAndSmiley()
                .UseSmartyPants()
                .UseAdvancedExtensions()
                .Build();

            // Step 1: Get all Markdown files and generate menu dynamically
            var markdownFiles = InputDirectory.GlobFiles("**/*.md");
            var menu = GenerateMenu(markdownFiles);
            
            // Step 2: Generate HTML for each Markdown file
            markdownFiles.ForEach(file => ProcessMarkdownFile(file, pipeline, templateContent, menu));
            
            Log.Information("HTML files generated successfully!");
        });

    Target CopyAssets => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            var inputAssets = InputDirectory / "assets";
            var outputAssets = OutputDirectory / "assets";
            CopyDirectory(inputAssets, outputAssets, "Assets");
        });
    
    Target CopyJsScripts => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            var templateScripts = TemplateDirectory / "js";
            var outputScripts = OutputDirectory / "js";
            CopyDirectory(templateScripts, outputScripts, "Scripts");
        });
    
    Target BuildWebsite => _ => _
        .DependsOn(CopyAssets, CopyJsScripts)
        .DependsOn<ISitemap>(x => x.GenerateSitemap)
        .DependsOn<IRobotsTxt>(x => x.GenerateRobotsTxt)
        .Executes(() =>
        {
            // Add more logic if necessary, like bundling, minifying, etc.
            Log.Information("Static website built successfully!");
        });
    
    
    private static List<MenuItem> GenerateMenu(IReadOnlyCollection<AbsolutePath> markdownFiles)
    {
        var menu = markdownFiles
            .Select(file => new MenuItem
            {
                Title = Path.GetFileNameWithoutExtension((string)file),
                Url = $"{Path.GetFileNameWithoutExtension((string)file)}.html"
            })
            // exclude index.html from the menu
            .Where(item => item.Url != "index.html")
            .ToList();
        
        return menu;
    }

    private void ProcessMarkdownFile(AbsolutePath file, MarkdownPipeline pipeline, string templateContent, List<MenuItem> menu)
    {
        try
        {
            Log.Information($"Processing {file}");

            var content = file.ReadAllText();

            // Parse the Markdown document
            var markdownDocument = Markdown.Parse(content, pipeline);

            // Extract YAML front matter
            var metadata = ExtractMetadata(markdownDocument, content);

            // Remove the YAML front matter from the content
            var markdownContent = RemoveFrontMatter(content, markdownDocument);

            // Convert Markdown to HTML
            var htmlContent = Markdown.ToHtml(markdownContent, pipeline);

            // Prepare data for the template
            var templateData = PrepareTemplateData(file, metadata, htmlContent, menu);

            // Replace placeholders in the template
            var finalHtml = Template
                .Parse(templateContent)
                .Render(templateData);

            var outputFile = OutputDirectory / $"{file.NameWithoutExtension}.html";
            outputFile.WriteAllText(finalHtml);

            Log.Information($"Generated HTML: {outputFile}");
        }
        catch (Exception ex)
        {
            Log.Error($"Error processing {file}: {ex.Message}");
            throw;
        }
    }

    private static Dictionary<string, string> ExtractMetadata(MarkdownDocument markdownDocument, string content)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
        var yamlBlock = markdownDocument.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock != null)
        {
            var yaml = content.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length);
            var input = new StringReader(yaml);
            var yamlStream = new YamlStream();
            yamlStream.Load(input);

            var rootNode = (YamlMappingNode)yamlStream.Documents[0].RootNode;
            foreach (var entry in rootNode.Children)
            {
                var key = ((YamlScalarNode)entry.Key).Value;
                var value = ((YamlScalarNode)entry.Value).Value;
                metadata[key] = value;
            }
        }

        return metadata;
    }
    
    private string RemoveFrontMatter(string content, MarkdownDocument markdownDocument)
    {
        var yamlBlock = markdownDocument.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock != null)
        {
            var contentStart = yamlBlock.Span.End + 1;
            return content.Substring(contentStart).TrimStart();
        }
        
        return content;
    }
    
    private object PrepareTemplateData(AbsolutePath file, Dictionary<string, string> metadata, string htmlContent, List<MenuItem> menu)
    {
        var pageUrl = new Uri(new Uri(SiteBaseUrl.TrimEnd('/') + "/"), $"{file.NameWithoutExtension}.html").AbsoluteUri;
        var imageUrl = metadata.TryGetValue("image", out var image) ? image : DefaultImageUrl;

        var templateData = new
        {
            site_title = SiteTitle,
            page_title = metadata.TryGetValue("title", out var title) ? title : file.NameWithoutExtension,
            description = metadata.TryGetValue("description", out var description) ? description : "",
            keywords = metadata.TryGetValue("keywords", out var keywords) ? keywords : "",
            author = metadata.TryGetValue("author", out var author) ? author : "",
            date = metadata.TryGetValue("date", out var date) ? date : "",
            content = htmlContent,
            page_url = pageUrl,
            image_url = imageUrl,
            menu
        };
        
        return templateData;
    }
    
    private void CopyDirectory(AbsolutePath source, AbsolutePath destination, string description)
    {
        Log.Information($"Copying {description.ToLower()}...");
        Log.Information($"Source: {source}");
        Log.Information($"Destination: {destination}");
        
        // Check if the source directory exists before attempting to copy
        if (source.DirectoryExists())
        {
            source.Copy(destination);
            Log.Information($"{description} copied successfully!");
        }
        else
        {
            Log.Warning($"{description} directory not found: {source}");
        }
    }
}

public record MenuItem
{
    public required string Title { get; init; }
    public required string Url { get; init; }
}