﻿using AngleSharp;
using AngleSharp.Html;

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace EpicDocSync
{
    internal class DevDocSync
    {
        internal static async Task Run(ChromeDriver chromeDriver, IBrowsingContext context)
        {
            var dir = new DirectoryInfo("docs");
            var baseUri = new Uri("https://dev.epicgames.com/docs");

            var linkQueue = new LinkQueue();
            linkQueue.Queue(baseUri);

            var errorCount = 0;

            while (linkQueue.TryDequeue(out var uri))
            {
            retry:

                if (errorCount >= 10)
                {
                    Console.WriteLine("::error::Aborting due to frequent server errors!");
                    return;
                }

                try
                {
                    Console.WriteLine($"[{linkQueue.Done} / {linkQueue.Total}] Updating {uri}");

                    if (uri.PathAndQuery.Contains("sessioninvalidated", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

#if DEBUG
                    if (uri.PathAndQuery.StartsWith("/docs/api-ref/"))
                    {
                        continue;
                    }
#endif
                    chromeDriver.Navigate().GoToUrl(uri);
                    chromeDriver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);

                    chromeDriver.FindElement(By.CssSelector("section.page"));

                    var pageSource = chromeDriver.PageSource;

                    if (pageSource.Contains("Too many requests, please try again later."))
                    {
                        errorCount++;
                        await Task.Delay(10000);
                        goto retry;
                    }

                    //if (pageSource.Contains("Sorry, you don't have access to this page!"))
                    //{
                    //    Console.WriteLine($"::error::No permission to {uri}");
                    //}

                    var document = await context.OpenAsync(req => req.Content(pageSource).Address(uri));

                    var page = document.QuerySelector("section.page");

                    if (page is null)
                    {
                        Console.WriteLine($"::error::Failed to parse {uri}");

                        continue;
                    }

                    var hero = page.QuerySelector("div.hero__header");
                    hero?.Remove();

                    var toolbox = page.QuerySelector("#toolbox_panel");
                    toolbox?.Remove();

                    var nav = page.QuerySelector("nav");
                    nav?.Remove();

                    foreach (var el in page.QuerySelectorAll("[style]"))
                    {
                        el.RemoveAttribute("style");
                    }

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

                    // Code Blocks
                    foreach (var codeBlock in page.QuerySelectorAll("pre"))
                    {
                        // Cleanup HTML, leaving text only
                        codeBlock.TextContent = codeBlock.TextContent;
                    }

                    // Search
                    page.QuerySelector("#search_container")?.Remove();

                    // Remove generated class names
                    foreach (var el in page.QuerySelectorAll("[class]"))
                    {
                        el.Attributes.RemoveNamedItem("class");
                    }

                    // Remove uselesss tags
                    foreach (var el in page.QuerySelectorAll("[data-mui-internal-clone-element]"))
                    {
                        el.Attributes.RemoveNamedItem("data-mui-internal-clone-element");
                    }

                    // Remove uselesss buttons
                    foreach (var el in page.QuerySelectorAll("button"))
                    {
                        if (el.ChildElementCount == 0)
                            el.Remove();
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

                    var str = page.ToHtml(new PrettyMarkupFormatter()).Trim();

                    var path = uri.AbsolutePath.Trim('/');
                    if (path.StartsWith("docs"))
                        path = path[4..].Trim('/');

                    if (path == "")
                    {
                        path = "index";
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

                    errorCount = 0;
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine($"::warning::{uri} fetch timeout");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Console.WriteLine(ex);
                }

                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }
}
