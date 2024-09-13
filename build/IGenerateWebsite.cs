using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Nuke.Common;
using Nuke.Common.IO;
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
            
            var menu = markdownFiles
                .Select(file => new
                {
                    title = Path.GetFileNameWithoutExtension((string)file),
                    url = $"{Path.GetFileNameWithoutExtension((string)file)}.html"
                })
                // exclude index.html from the menu
                .Where(item => item.url != "index.html")
                .ToList();
            
            // Step 2: Generate HTML for each Markdown file
            Parallel.ForEach(markdownFiles, file =>
            {
                Log.Information($"Generating HTML from {file}");

                var content = file.ReadAllText();

                // Parse the Markdown document
                var markdownDocument = Markdown.Parse(content, pipeline);

                // Extract YAML front matter
                var yamlBlock = markdownDocument.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
                var metadata = new Dictionary<string, string>();
                
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

                // Remove the YAML front matter from the content
                var markdownContent = content;
                if (yamlBlock != null)
                {
                    var contentStart = yamlBlock.Span.End + 1;
                    markdownContent = content.Substring(contentStart);
                }

                var htmlContent = Markdown.ToHtml(markdownContent, pipeline);

                // Prepare data for the template
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
                
                // Replace placeholders in the template
                var finalHtml = Template
                    .Parse(templateContent)
                    .Render(templateData);

                var outputFile = OutputDirectory / $"{file.NameWithoutExtension}.html";
                outputFile.WriteAllText(finalHtml);

                Log.Information($"HTML generated successfully: {outputFile}");
            });
            
            Log.Information("HTML files generated successfully!");
        });
    
    Target CopyAssets => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            var inputAssets = InputDirectory / "assets";
            var outputAssets = OutputDirectory / "assets";
            
            Log.Information("Copying assets...");
            Log.Information($"Input assets directory: {inputAssets}");
            Log.Information($"Output assets directory: {outputAssets}");
        
            // Check if the input assets directory exists before attempting to copy
            if (inputAssets.DirectoryExists())
            {
                inputAssets.Copy(outputAssets);
                Log.Information("Assets copied successfully!");
            }
            else
            {
                Log.Warning($"Assets directory not found: {inputAssets}");
            }
        });
    
    Target CopyJsScripts => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            var templateScripts = TemplateDirectory / "js";
            var outputScripts = OutputDirectory / "js";
            
            Log.Information("Copying scripts...");
            Log.Information($"Template scripts directory: {templateScripts}");
            Log.Information($"Output scripts directory: {outputScripts}");
        
            // Check if the input scripts directory exists before attempting to copy
            if (templateScripts.DirectoryExists())
            {
                templateScripts.Copy(outputScripts);
                Log.Information("Scripts copied successfully!");
            }
            else
            {
                Log.Warning($"Scripts directory not found: {templateScripts}");
            }
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
}