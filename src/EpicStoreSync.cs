using AngleSharp;
using AngleSharp.Dom;

using Esprima;

using OpenQA.Selenium.Chrome;

using System.Text.RegularExpressions;

internal class EpicStoreSync
{


    internal static async Task Run(ChromeDriver chromeDriver, IBrowsingContext context)
    {
        var dir = new DirectoryInfo("graphql");
        dir.Create();

        var uri = "https://store.epicgames.com/";
        chromeDriver.Navigate().GoToUrl(uri);
        chromeDriver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(15);
        var pageSource = chromeDriver.PageSource;


        var document = await context.OpenAsync(req => req.Content(pageSource).Address(uri));
        var scriptTags = document.QuerySelectorAll("script");
        var scriptsFound = 0;

        foreach (var tag in scriptTags )
        {
            var src = tag.Attributes["src"]?.Value;

            if (src == null) 
                continue;

            if (src.Contains("ClientIndex"))
            {
                scriptsFound++;
                await ParseScript(src).ConfigureAwait(false);
            }
        }

        if (scriptsFound == 0)
        {
            Console.WriteLine($"::error::No scripts found!");
            return;
        }

        foreach (var item in Directory.GetFiles(dir.FullName, "*.graphql"))
        {
            File.Delete(item);
        }

        foreach (var kv in FoundQueries)
        {
            var i = 1;

            foreach (var item in kv.Value.Order())
            {
                var fileName = kv.Value.Count == 1 ? $"{kv.Key}.graphql" : $"{kv.Key}_{i++}.graphql";
                var fullName = Path.Combine(dir.FullName, fileName);

                await File.WriteAllTextAsync(fullName, item).ConfigureAwait(false);
            }
        }
    }

    static Regex GraphQLString = new("((query|mutation) (?<name>[a-zA-Z0-9]+)\\(|fragment (?<name>[a-zA-Z0-9]+) on (?<parent>[a-zA-Z0-9]+))");

    static Dictionary<string, List<string>> FoundQueries = new Dictionary<string, List<string>>();

    internal static void AddQuery(string name, string query)
    {
        name = name.ToLowerInvariant();
        if (!FoundQueries.ContainsKey(name))
        {
            FoundQueries[name] = new List<string>();
        }

        FoundQueries[name].Add(query);
    }

    internal static async Task ParseScript(string uri)
    {
        var httpClient = new HttpClient();
        var script = await httpClient.GetStringAsync(uri).ConfigureAwait(false);

        var parser = new JavaScriptParser(new ParserOptions
        {
            Tokens = true,
        });

        var parsed = parser.ParseScript(script);

        foreach (var token in parsed.Tokens!)
        {
            if (token.Type == TokenType.StringLiteral)
            {
                var m = GraphQLString.Match(token.Value);

                if (!m.Success)
                    continue;

                var query = token.Value[1..^1].Replace("\\n", "\n");
                var name = m.Groups["parent"].Success ? $"{m.Groups["parent"].Value}_{m.Groups["name"].Value}" : m.Groups["name"].Value;

                AddQuery(name, query);
            }
            else if (token.Type == TokenType.Template)
            {
                var m = GraphQLString.Match(token.Value);

                if (!m.Success)
                    continue;

                var query = token.Value[1..^1].Trim();
                var name = m.Groups["parent"].Success ? $"{m.Groups["parent"].Value}_{m.Groups["name"].Value}" : m.Groups["name"].Value;

                AddQuery(name, query);
            }
        }
    }
}