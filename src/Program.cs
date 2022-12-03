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

        foreach (var link in page.QuerySelectorAll("a"))
        {
            if (link.Attributes["href"]?.Value.StartsWith("/docs") == true)
            {
                var fullUri = new Uri(baseUri, link.Attributes["href"].Value!);
                linkQueue.Queue(fullUri);
            }
        }

        var str = page.ToHtml(new PrettyMarkupFormatter());

        var path = uri.AbsolutePath.Trim('/');
        if (path.StartsWith("docs"))
            path = path[4..].Trim('/');

        if (path == "")
        {
            path = "index.html";
        }
        else
        {
            path += ".html";
        }

        var file = Path.Combine(dir.FullName, path);

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
