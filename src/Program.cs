using AngleSharp;
using AngleSharp.Html;

using EpicDocSync;

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

using System.Globalization;


// Config
CultureInfo.CurrentCulture = new CultureInfo("en-US");

var chromeOptions = new ChromeOptions();
chromeOptions.AddArgument("headless");
chromeOptions.AddArgument("user-agent=EpicGamesLauncher/14.0.8-22004686+++Portal+Release-Live");

var chromeDriver = new ChromeDriver(chromeOptions);
var config = Configuration.Default;
var context = BrowsingContext.New(config);

// GraphQL
await EpicStoreSync.Run(chromeDriver, context).ConfigureAwait(false);

// Docs
await DevDocSync.Run(chromeDriver, context).ConfigureAwait(false);

try
{
    chromeDriver.Close();
}
catch (Exception)
{
}