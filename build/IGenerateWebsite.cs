using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Markdig;
using Nuke.Common;
using Nuke.Common.IO;
using Scriban;
using Serilog;

public interface IGenerateWebsite : IHasWebsitePaths
{
    [Parameter("Title of the site")][Required]
    string SiteTitle => TryGetValue(() => SiteTitle);
    
    Target GenerateHtml => _ => _
        .DependsOn<IClean>(x => x.Clean)
        .Executes(() =>
        {
            Log.Information("Generating HTML files from Markdown...");
            
            var template = TemplateDirectory / "template.html";
            var templateContent = template.ReadAllText();
            
            // Use advanced extensions for Markdown processing
            var pipeline = new MarkdownPipelineBuilder()
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
                var htmlContent = Markdown.ToHtml(content, pipeline);

                // Replace placeholders in the template
                var finalHtml = Template
                    .Parse(templateContent)
                    .Render(new
                    {
                        site_title = SiteTitle,
                        page_title = file.NameWithoutExtension,
                        content = htmlContent,
                        menu
                    });

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
        .DependsOn(GenerateHtml, CopyAssets, CopyJsScripts)
        .Executes(() =>
        {
            // Add more logic if necessary, like bundling, minifying, etc.
            Log.Information("Static website built successfully!");
        });
}