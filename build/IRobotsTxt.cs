using Nuke.Common;
using Nuke.Common.IO;
using Scriban;
using static Serilog.Log;

public interface IRobotsTxt : IHasWebsitePaths
{
    Target GenerateRobotsTxt => _ => _
        .DependsOn<IGenerateWebsite>(x => x.GenerateHtml)
        .TriggeredBy<IGenerateWebsite>(x => x.GenerateHtml)
        .Executes(() =>
        {
            Information("Generating robots.txt...");

            var robotsTemplate = TemplateDirectory / "robots.txt";
            if (robotsTemplate.FileExists())
            {
                var content = robotsTemplate.ReadAllText();
                var processedContent = Template
                    .Parse(content)
                    .Render(new { site_base_url = SiteBaseUrl.TrimEnd('/') });

                var outputFile = OutputDirectory / "robots.txt";
                outputFile.WriteAllText(processedContent);

                Information("robots.txt generated at {OutputFile}", outputFile);
            }
            else
            {
                Warning("robots.txt template not found at {RobotsTemplate}", robotsTemplate);
            }
        });
}