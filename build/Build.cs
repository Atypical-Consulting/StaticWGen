using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.Docker;
using Markdig;
using Octokit;
using Scriban;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Serilog.Log;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.DeployDockerImage);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    
    [Parameter] readonly string ImageName;
    [Parameter] readonly string VersionTag;
    [Parameter] readonly string ContainerName;
    [Parameter] readonly int HostPort;
    [Parameter] readonly int ContainerPort;
    [Parameter] readonly string SiteTitle;
    
    AbsolutePath InputDirectory => RootDirectory / "input";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Executes(() =>
        {
            Information("Cleaning output directory...");
            OutputDirectory.CreateOrCleanDirectory();
            Information("Output directory cleaned successfully!");
        });

    Target GenerateHtml => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            Information("Generating HTML files from Markdown...");
            
            var template = InputDirectory / "template.html";
            var templateContent = template.ReadAllText();

            // Step 1: Get all Markdown files and generate menu dynamically
            var markdownFiles = InputDirectory.GlobFiles("**/*.md");
            
            var menu = markdownFiles
                .Select(file => new
                {
                    title = Path.GetFileNameWithoutExtension(file),
                    url = $"{Path.GetFileNameWithoutExtension(file)}.html"
                })
                .ToList();
            
            // Step 2: Generate HTML for each Markdown file
            markdownFiles.ForEach(file =>
            {
                Information($"Generating HTML from {file}");

                var content = file.ReadAllText();
                var htmlContent = Markdown.ToHtml(content);

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

                var outputFile = OutputDirectory / $"{Path.GetFileNameWithoutExtension(file)}.html";
                outputFile.WriteAllText(finalHtml);

                Information($"HTML generated successfully: {outputFile}");
            });
            
            Information("HTML files generated successfully!");
        });
    
    Target CopyAssets => _ => _
        .DependsOn(GenerateHtml)
        .Executes(() =>
        {
            var inputAssets = InputDirectory / "assets";
            var outputAssets = OutputDirectory / "assets";
            
            Information("Copying assets...");
            Information($"Input assets directory: {inputAssets}");
            Information($"Output assets directory: {outputAssets}");
        
            // Check if the input assets directory exists before attempting to copy
            if (inputAssets.DirectoryExists())
            {
                inputAssets.Copy(outputAssets);
                Information("Assets copied successfully!");
            }
            else
            {
                Warning($"Assets directory not found: {inputAssets}");
            }
        });
    
    Target BuildWebsite => _ => _
        .DependsOn(CopyAssets)
        .Executes(() =>
        {
            // Add more logic if necessary, like bundling, minifying, etc.
            Information("Static website built successfully!");
        });

    Target BuildDockerImage => _ => _
        .DependsOn(BuildWebsite)
        .Executes(() =>
        {
            DockerLogger = (type, text) => Debug(text);
            
            DockerBuild(x => x
                .SetPath(RootDirectory)
                .SetTag($"{ImageName}:{VersionTag}")
            );
            
            Information($"Docker image {ImageName}:{VersionTag} built successfully!");
        });
    
    Target DeployDockerImage => _ => _
        .DependsOn(BuildDockerImage)
        .Executes(() =>
        {
            // Stop and remove any running container with the same name
            if (DockerPs().Any(x => x.Text.Contains(ContainerName)))
            {
                Information($"Stopping and removing existing container {ContainerName}...");
                DockerStop(x => x.SetContainers(ContainerName));
                DockerRm(x => x.SetForce(true).SetContainers(ContainerName));
                Information($"Existing container {ContainerName} stopped and removed successfully!");
            }

            // Run the new container
            DockerRun(x => x
                .SetDetach(true)
                .SetPublish($"{HostPort}:{ContainerPort}")
                .SetName(ContainerName)
                .SetImage($"{ImageName}:{VersionTag}")
                .SetProcessLogOutput(true)
                .SetProcessLogInvocation(true)
            );
        
            Information($"Docker container {ContainerName} running at http://localhost:{HostPort}");
        });
}
