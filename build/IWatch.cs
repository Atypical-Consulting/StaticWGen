using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Nuke.Common;
using Nuke.Common.IO;
using Scriban;
using static Serilog.Log;

public interface IWatch : IHasWebsitePaths
{
    [Parameter("Port for the development server")]
    int Port => TryGetValue<int?>(() => Port) ?? 3000;

    Target Watch => _ => _
        .DependsOn<IGenerateWebsite>(x => x.BuildWebsite)
        .Executes(() =>
        {
            Information("Starting watch mode...");

            // Inject live-reload script into all HTML files
            InjectLiveReloadScript();

            // Start HTTP server in background
            var httpThread = new Thread(() => StartHttpServer());
            httpThread.IsBackground = true;
            httpThread.Start();

            Information("Serving at http://localhost:{Port}", Port);
            Information("Watching input/ and template/ for changes...");
            Information("Press Ctrl+C to stop.");

            // Track last rebuild to debounce
            var lastRebuild = DateTime.MinValue;
            var debounceMs = 300;

            // Watch input directory
            using var inputWatcher = new FileSystemWatcher(InputDirectory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            // Watch template directory
            using var templateWatcher = new FileSystemWatcher(TemplateDirectory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            var rebuildLock = new object();

            void OnChange(object sender, FileSystemEventArgs e)
            {
                lock (rebuildLock)
                {
                    if ((DateTime.Now - lastRebuild).TotalMilliseconds < debounceMs)
                        return;
                    lastRebuild = DateTime.Now;
                }

                var isTemplate = e.FullPath.StartsWith(TemplateDirectory);
                var fileName = Path.GetFileName(e.FullPath);

                if (isTemplate)
                {
                    Information("[{Time:HH:mm:ss}] Template changed: {File} — full rebuild",
                        DateTime.Now, fileName);
                    FullRebuild();
                }
                else if (e.FullPath.EndsWith(".md"))
                {
                    Information("[{Time:HH:mm:ss}] Content changed: {File} — rebuilding",
                        DateTime.Now, fileName);
                    RebuildSingleFile((AbsolutePath)e.FullPath);
                }
            }

            inputWatcher.Changed += OnChange;
            inputWatcher.Created += OnChange;
            inputWatcher.Deleted += (s, e) => { OnChange(s, e); };
            templateWatcher.Changed += OnChange;
            templateWatcher.Created += OnChange;

            // Block forever (until Ctrl+C)
            using var resetEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                resetEvent.Set();
            };
            resetEvent.WaitOne();

            Information("Watch mode stopped.");
        });

    private void FullRebuild()
    {
        try
        {
            var sw = Stopwatch.StartNew();

            // Re-copy static assets (JS/CSS) from template to output
            CopyTemplateAssets();

            var templateContent = (TemplateDirectory / "template.html").ReadAllText();
            var markdownFiles = InputDirectory.GlobFiles("**/*.md");
            var menu = markdownFiles
                .Select(f => new { Title = f.NameWithoutExtension, Url = $"{f.NameWithoutExtension}.html" })
                .Where(i => i.Url != "index.html" && i.Url != "404.html")
                .ToList();

            foreach (var file in markdownFiles)
            {
                RebuildMarkdownFile(file, templateContent);
            }

            InjectLiveReloadScript();
            sw.Stop();
            Information("[{Time:HH:mm:ss}] Full rebuild completed ({Duration}ms)",
                DateTime.Now, sw.ElapsedMilliseconds);
            // Signal live reload
            Interlocked.Increment(ref _reloadVersion);
        }
        catch (Exception ex)
        {
            Error("[{Time:HH:mm:ss}] Rebuild failed: {Message}", DateTime.Now, ex.Message);
        }
    }

    private void RebuildSingleFile(AbsolutePath file)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var templateContent = (TemplateDirectory / "template.html").ReadAllText();
            RebuildMarkdownFile(file, templateContent);

            // Inject live-reload into the rebuilt file
            var outputFile = OutputDirectory / $"{file.NameWithoutExtension}.html";
            if (outputFile.FileExists())
            {
                var html = outputFile.ReadAllText();
                html = html.Replace("</body>", LiveReloadScript + "</body>");
                outputFile.WriteAllText(html);
            }

            sw.Stop();
            Information("[{Time:HH:mm:ss}] Rebuilt {File} ({Duration}ms)",
                DateTime.Now, file.Name, sw.ElapsedMilliseconds);
            Interlocked.Increment(ref _reloadVersion);
        }
        catch (Exception ex)
        {
            Error("[{Time:HH:mm:ss}] Error rebuilding {File}: {Message}",
                DateTime.Now, file.Name, ex.Message);
        }
    }

    private void RebuildMarkdownFile(AbsolutePath file, string templateContent)
    {
        var content = file.ReadAllText();
        var pipeline = MarkdownHelper.CreatePipeline();

        var writer = new StringWriter();
        var htmlRenderer = new HtmlRenderer(writer);
        pipeline.Setup(htmlRenderer);
        var defaultRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();
        if (defaultRenderer != null)
            htmlRenderer.ObjectRenderers.Remove(defaultRenderer);
        htmlRenderer.ObjectRenderers.AddIfNotAlready(new MermaidAwareCodeBlockRenderer());

        var document = Markdown.Parse(content, pipeline);
        var metadata = MarkdownHelper.ExtractMetadata(document, content, file.Name);
        var markdownContent = MarkdownHelper.RemoveFrontMatter(document, content);

        var doc2 = Markdown.Parse(markdownContent, pipeline);
        htmlRenderer.Render(doc2);
        writer.Flush();
        var htmlContent = writer.ToString();

        var pageTitle = metadata.TryGetValue("title", out var t) ? t : file.NameWithoutExtension;
        var templateData = new
        {
            site_title = SiteTitle,
            page_title = pageTitle,
            description = metadata.TryGetValue("description", out var d) ? d : "",
            keywords = metadata.TryGetValue("keywords", out var k) ? k : "",
            author = metadata.TryGetValue("author", out var a) ? a : "",
            date = metadata.TryGetValue("date", out var dt) ? dt : "",
            iso_date = "",
            og_type = "website",
            schema_type = "WebPage",
            noindex = false,
            content = htmlContent,
            toc = "",
            page_url = $"{SiteBaseUrl.TrimEnd('/')}/{file.NameWithoutExtension}.html",
            canonical_url = $"{SiteBaseUrl.TrimEnd('/')}/{file.NameWithoutExtension}.html",
            image_url = "",
            analytics_snippet = "",
            base_path = BasePath,
            menu = InputDirectory.GlobFiles("**/*.md")
                .Select(f => new { title = f.NameWithoutExtension, url = $"{f.NameWithoutExtension}.html" })
                .Where(i => i.url != "index.html" && i.url != "404.html")
                .ToList(),
            tags = new List<object>()
        };

        var finalHtml = Template.Parse(templateContent).Render(templateData);
        var outputFile = OutputDirectory / $"{file.NameWithoutExtension}.html";
        outputFile.WriteAllText(finalHtml);
    }

    private void InjectLiveReloadScript()
    {
        var htmlFiles = OutputDirectory.GlobFiles("**/*.html").ToList();
        foreach (var file in htmlFiles)
        {
            var html = file.ReadAllText();
            if (!html.Contains("__live_reload"))
            {
                html = html.Replace("</body>", LiveReloadScript + "</body>");
                file.WriteAllText(html);
            }
        }
    }

    private static int _reloadVersion = 0;

    private static string LiveReloadScript => """
        <script id="__live_reload">
        (function(){
          var v = 0;
          setInterval(function(){
            fetch('/__reload')
              .then(function(r){ return r.text(); })
              .then(function(newV){
                if (v && newV !== String(v)) location.reload();
                v = parseInt(newV);
              })
              .catch(function(){});
          }, 1000);
        })();
        </script>
        """;

    private void StartHttpServer()
    {
        try
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{Port}/");
            listener.Start();

            while (listener.IsListening)
            {
                var context = listener.GetContext();
                var request = context.Request;
                var response = context.Response;

                try
                {
                    if (request.Url!.AbsolutePath == "/__reload")
                    {
                        var versionBytes = Encoding.UTF8.GetBytes(_reloadVersion.ToString());
                        response.ContentType = "text/plain";
                        response.ContentLength64 = versionBytes.Length;
                        response.OutputStream.Write(versionBytes);
                    }
                    else
                    {
                        var path = request.Url.AbsolutePath.TrimStart('/');
                        if (string.IsNullOrEmpty(path)) path = "index.html";
                        if (!path.Contains('.')) path += "/index.html";

                        var filePath = OutputDirectory / path;
                        if (filePath.FileExists())
                        {
                            var fileBytes = File.ReadAllBytes(filePath);
                            response.ContentType = GetContentType(path);
                            response.ContentLength64 = fileBytes.Length;
                            response.OutputStream.Write(fileBytes);
                        }
                        else
                        {
                            // Serve 404 page
                            var notFoundPath = OutputDirectory / "404.html";
                            response.StatusCode = 404;
                            if (notFoundPath.FileExists())
                            {
                                var bytes = File.ReadAllBytes(notFoundPath);
                                response.ContentType = "text/html";
                                response.ContentLength64 = bytes.Length;
                                response.OutputStream.Write(bytes);
                            }
                        }
                    }
                }
                catch { response.StatusCode = 500; }
                finally { response.Close(); }
            }
        }
        catch (Exception ex)
        {
            Error("HTTP server error: {Message}", ex.Message);
        }
    }

    private void CopyTemplateAssets()
    {
        var sourceJs = TemplateDirectory / "js";
        var sourceCss = TemplateDirectory / "css";
        var outputJs = OutputDirectory / "js";
        var outputCss = OutputDirectory / "css";

        if (sourceJs.DirectoryExists())
        {
            if (outputJs.DirectoryExists())
                outputJs.DeleteDirectory();
            sourceJs.Copy(outputJs);
        }

        if (sourceCss.DirectoryExists())
        {
            if (outputCss.DirectoryExists())
                outputCss.DeleteDirectory();
            sourceCss.Copy(outputCss);
        }
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream"
        };
    }
}
