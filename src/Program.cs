using AngleSharp;
using AngleSharp.Html;

using EpicDocSync;

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;


// Config
var dir = new DirectoryInfo("docs");
CultureInfo.CurrentCulture = new CultureInfo("en-US");

var httpClient = new HttpClient();

// Angle Sharp
var config = Configuration.Default;
var context = BrowsingContext.New(config);

var baseUri = new Uri("https://dev.epicgames.com/docs");
var linkQueue = new LinkQueue();

linkQueue.Queue(baseUri);

while (linkQueue.TryDequeue(out var uri))
{
    try
    {
        Console.WriteLine($"[{linkQueue.Done} / {linkQueue.Total}] Updating {uri}");

        if (uri.PathAndQuery.Contains("sessioninvalidated", StringComparison.InvariantCultureIgnoreCase))
        {
            continue;
        }

        var response = await httpClient.GetAsync(uri).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var document = await context.OpenAsync(req => req.Content(content).Address(uri));

        var page = document.QuerySelector("section.page");

        if (page is null)
            continue;

        foreach (var style in page.QuerySelectorAll("style"))
        {
            style.Remove();
        }

        foreach (var footer in page.QuerySelectorAll("footer"))
        {
            footer.Remove();
        }

        foreach (var image in page.QuerySelectorAll("svg"))
        {
            image.Remove();
        }

        // Search
        page.QuerySelector("#search_container")?.Remove();

        // Remove generated class names
        foreach (var el in page.QuerySelectorAll("[class]"))
        {
            el.Attributes.RemoveNamedItem("class");
        }

        // 
        foreach (var link in page.QuerySelectorAll("a"))
        {
            var attrib = link.Attributes["href"];

            if (attrib is null)
            {
                continue;
            }

            if (attrib.Value.StartsWith("/docs") == true)
            {
                var href = attrib.Value.Replace("/en-US/", "/");

                if (href == null || href.Contains("site-map?tag="))
                    continue;

                var hashPos = href.IndexOf("#");
                string hash = "";

                if (hashPos > -1)
                {
                    hash = href[hashPos..];
                    href = href[..hashPos];
                }

                if (href.EndsWith("/index"))
                {
                    href = href[..^6];
                }

                var fullUri = new Uri(baseUri, href);

                attrib.Value = fullUri.AbsolutePath + hash;

                if (fullUri.Query != "")
                    continue;

                linkQueue.Queue(fullUri);
            }
        }

        var str = page.ToHtml(new PrettyMarkupFormatter());

        var path = uri.AbsolutePath.Trim('/');
        if (path.StartsWith("docs"))
            path = path[4..].Trim('/');

        if (path == "")
        {
            path = "index";
        }
        else
        {
            //path += ".html";
        }

        var file = Path.Combine(dir.FullName, path);

        if (Directory.Exists(file))
        {
            var oldFile = file + ".html";

            file += "/index";

            if (File.Exists(oldFile))
            {
                File.Delete(oldFile);
            }
        }

        file += ".html";

        var dirName = Path.GetDirectoryName(file)!;

        if (!Directory.Exists(dirName))
        {
            Directory.CreateDirectory(dirName);
        }

        await File.WriteAllTextAsync(file, str).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
    }   
}
