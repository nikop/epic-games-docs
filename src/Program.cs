using AngleSharp;
using AngleSharp.Html;

using EpicDocSync;

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

using System.Globalization;


// Config
CultureInfo.CurrentCulture = new CultureInfo("en-US");

var chromeOptions = new ChromeOptions();
#if !DEBUG
chromeOptions.AddArgument("headless");
#endif

var chromeDriver = new ChromeDriver(chromeOptions);
var config = Configuration.Default;
var context = BrowsingContext.New(config);

// GraphQL
await EpicStoreSync.Run(chromeDriver, context).ConfigureAwait(false);

#if !DEBUG
// Docs
await DevDocSync.Run(chromeDriver, context).ConfigureAwait(false);
#endif

chromeDriver.Close();