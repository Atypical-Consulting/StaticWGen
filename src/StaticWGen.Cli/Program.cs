using System.Globalization;
using System.Reflection;
using Nuke.Common.IO;
using Serilog;
using StaticWGen.Core;
using YamlDotNet.RepresentationModel;

const string ConfigFile = "staticwgen.yaml";
const string Usage = """
    Usage: staticwgen <command> [options]

    Commands:
      new <dir>          Create a site in <dir>: themes/, input/ and staticwgen.yaml
      build              Generate the site into output/ (validated, minified, zipped)
      serve              Build, then serve on --port (default 3000) and rebuild on change
      new-post <title>   Add a dated post to input/   (--tags "a, b" --author "Me")
      new-page <title>   Add a page to input/

    Every site setting is both an option and a staticwgen.yaml key, e.g.
      --site-title / site_title    --site-base-url / site_base_url    --theme / theme
      --include-drafts             --port 8080                        --minify-html false
    Options override staticwgen.yaml. Run commands from the site's folder.
    """;

Log.Logger = new LoggerConfiguration().WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}{Exception}").CreateLogger();

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Usage);
    return 0;
}

try
{
    var command = args[0];
    var (positional, options) = Parse(args[1..]);
    switch (command)
    {
        case "new":
            CreateSite(Single(positional, "new <dir>"), options);
            return 0;
        case "build":
            LoadSite(options).Build();
            return 0;
        case "serve":
            var site = LoadSite(options);
            site.Build();
            site.Watch();
            return 0;
        case "new-post":
        case "new-page":
            var tags = Take(options, "tags");
            var author = Take(options, "author");
            var title = Single(positional, $"{command} <title>");
            if (command == "new-post") LoadSite(options).NewPost(title, tags, author);
            else LoadSite(options).NewPage(title, tags, author);
            return 0;
        default:
            throw new ArgumentException($"Unknown command '{command}'. Run 'staticwgen --help'.");
    }
}
catch (Exception ex)
{
    Log.Error("{Message}", ex.Message);
    return 1;
}

// `staticwgen new <dir>`: bundled themes + starter content + a config file.
static void CreateSite(string dir, Dictionary<string, string> options)
{
    var root = (AbsolutePath)Path.GetFullPath(dir);
    if (root.DirectoryExists() && Directory.EnumerateFileSystemEntries(root).Any())
        throw new ArgumentException($"{root} already exists and is not empty.");

    var title = Take(options, "site-title") is { Length: > 0 } t ? t : root.Name;
    var baseUrl = Take(options, "site-base-url") is { Length: > 0 } u ? u : "http://localhost:3000";
    var author = Take(options, "author");

    ((AbsolutePath)AppContext.BaseDirectory / "themes").Copy(root / "themes");
    (root / ConfigFile).WriteAllText($"""
        site_title: "{title}"
        site_base_url: "{baseUrl}"
        theme: default
        """);
    (root / ".gitignore").WriteAllText("output/\nsite.zip\nbuild-report.json\n");
    new Site(root).Init(title, author);

    Log.Information("Created {Root}. Next: cd {Dir} && staticwgen serve", root, dir);
}

// Site settings: staticwgen.yaml first, then command-line options on top.
static Site LoadSite(Dictionary<string, string> options)
{
    var root = (AbsolutePath)Directory.GetCurrentDirectory();
    var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    if ((root / ConfigFile).FileExists())
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader((root / ConfigFile).ReadAllText()));
        if (yaml.Documents.FirstOrDefault()?.RootNode is YamlMappingNode map)
            foreach (var (key, value) in map.Children)
                settings[key.ToString()] = value.ToString();
    }
    foreach (var (key, value) in options)
        settings[key] = value;

    var site = new Site(root);
    var properties = typeof(Site).GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanWrite)
        .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    foreach (var (key, value) in settings)
    {
        var name = key.Replace("-", "").Replace("_", "");
        if (!properties.TryGetValue(name, out var property))
            throw new ArgumentException($"Unknown setting '{key}'. Run 'staticwgen --help'.");
        property.SetValue(site, Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture));
    }
    return site;
}

// args after the command: `--name value`, a bare `--flag` (= true), or positional values.
static (List<string> Positional, Dictionary<string, string> Options) Parse(string[] rest)
{
    var positional = new List<string>();
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < rest.Length; i++)
    {
        if (!rest[i].StartsWith("--"))
        {
            positional.Add(rest[i]);
            continue;
        }
        var name = rest[i][2..];
        var hasValue = i + 1 < rest.Length && !rest[i + 1].StartsWith("--");
        options[name] = hasValue ? rest[++i] : "true";
    }
    return (positional, options);
}

static string Take(Dictionary<string, string> options, string name) =>
    options.Remove(name, out var value) ? value : "";

static string Single(List<string> positional, string usage) =>
    positional.Count == 1 ? positional[0] : throw new ArgumentException($"Usage: staticwgen {usage}");
