using Nuke.Common;
using Nuke.Common.IO;
using Scriban;
using static Serilog.Log;

namespace StaticWGen.Core;

public partial class Site
{
    public void GenerateRobotsTxt()
    {
        Information("Generating robots.txt...");

        var robotsTemplate = ThemePath("robots.txt");
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
    }
}
