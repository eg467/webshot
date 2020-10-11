using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using WebshotService.Entities;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace WebshotService.Screenshotter
{
    public sealed class ChromeDriverAdapter : IDisposable
    {
        private const ScreenshotImageFormat _imageFormat = ScreenshotImageFormat.Png;

        /// <summary>
        /// The image file extension of screenshots, e.g. ".png".
        /// </summary>
        public static readonly string ImageExtension = $".{_imageFormat.ToString().ToLower()}";

        private const string AuthExtensionPath = "authextension.zip";

        private readonly ChromeDriver _driver;
        private readonly ScreenshotOptions _options;
        private readonly Dictionary<Uri, BrokenLink> _brokenLinks;
        private readonly ProjectCredentials _projectCredentials;

        public ChromeDriverAdapter(
            ScreenshotOptions? options = null,
            IEnumerable<BrokenLink>? brokenLinks = null,
            ProjectCredentials? projectCredentials = null)
        {
            _options = options ?? new ScreenshotOptions();
            brokenLinks ??= Enumerable.Empty<BrokenLink>();
            _brokenLinks = brokenLinks.ToDictionary(x => x.Target, x => x);
            _projectCredentials = projectCredentials ?? new ProjectCredentials();
            _driver = CreateDriver();
        }

        public void Dispose()
        {
            if (File.Exists(AuthExtensionPath))
            {
                File.Delete(AuthExtensionPath);
            }
            _driver.Quit();
            _driver.Dispose();
        }

        private ChromeDriver CreateDriver()
        {
            var options = new ChromeOptions();
            options.IncognitoMode();
            if (_projectCredentials.CredentialsByDomain.Any())
            {
                options.GenerateBasicAuthenticationExtension(AuthExtensionPath, _projectCredentials);
            }
            var driver = new ChromeDriver(options);
            return driver;
        }

        /// <summary>
        /// Takes a screenshot of a web page.
        /// </summary>
        /// <param name="url">The URL of the web page to screenshoot.</param>
        /// <param name="filePath">The image file to save to.</param>
        /// <param name="width">The device width of the browser. 0 for auto-sized width.</param>
        /// <returns>The full file path of the saved image</returns>
        public NavigationTiming TakeScreenshot(string url, string filePath, int width = 0)
        {
            var outputDir = Path.GetDirectoryName(filePath)
                ?? throw new DirectoryNotFoundException(filePath);
            Directory.CreateDirectory(outputDir);
            _driver.Navigate().GoToUrl(url);

            _driver.ResizeWindow(width);
            if (_options.HighlightBrokenLinks && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                HighlightBrokenLinks(uri);
            }

            // Sometimes, when this isn't high enough, the performance timings return incomplete data.
            int screenshotDelay = 3000;
            Thread.Sleep(screenshotDelay);

            var requestStats = GetRequestStats();
            var screenshot = _driver.GetScreenshot();
            screenshot.SaveAsFile(filePath, _imageFormat);
            _driver.ClearResize();
            return requestStats;

            // LOCAL FUNCTIONS

            NavigationTiming GetRequestStats()
            {
                var stats = (string)_driver.ExecuteScript(
                    @"return (window && window.performance && JSON.stringify([...window.performance.getEntriesByType('navigation'),{ }][0])) || '{}'");
                var deprecatedTiming = (string)_driver.ExecuteScript(
                    @"return (window && window.performance && window.performance.timing && JSON.stringify(window.performance.timing)) || '{}'");

                static string ConvertToInt(Match m)
                {
                    var origValue = m.Value;
                    if (!double.TryParse(origValue, out var dblVal)) return origValue;
                    var intValue = (int)Math.Round(dblVal);
                    return intValue.ToString();
                }

                stats = Regex.Replace(stats, @"\d+\.\d+", ConvertToInt);
                return JsonConvert.DeserializeObject<NavigationTiming>((string)stats);
            }

            void HighlightBrokenLinks(Uri rawCallingUri)
            {
                var standardizedUri = rawCallingUri.TryStandardize();
                var brokenHrefs =
                    _brokenLinks
                        .SelectMany(l => l.Value.Sources)
                        .Where(x => standardizedUri.Equals(x.CallingPage))
                        .Select(link => $@"a[href='{link.Href}']");

                _driver.HighlightElements(brokenHrefs);
            }
        }
    }

    public static class ChromeDriverExtensions
    {
        /// <summary>
        /// Repeatedly resize the window to a given width and the full document height to account for lazily loaded elements.
        /// </summary>
        /// <param name="width">The width of the window in pixels, 0 for auto width.</param>
        public static void ResizeWindow(this ChromeDriver driver, int width = 0)
        {
            // Adapted from https://stackoverflow.com/a/56535317
            int numTries = 0;
            const int maxTries = 8;
            int prevHeight;
            int calculatedHeight = -1;
            string autoWidthCommand =
                    @"return Math.max(
                        window.innerWidth,
                        document.body.scrollWidth,
                        document.documentElement.scrollWidth)";

            // Repeatedly resize height to allow new elements to (lazily) load.
            do
            {
                prevHeight = calculatedHeight;
                var calculatedWidth = width > 0 ? $"return {width}" : autoWidthCommand;
                calculatedHeight = CalculateDocHeight();

                // TODO: Set device-specific user agents.
                Dictionary<string, object> metrics = new Dictionary<string, object>
                {
                    ["width"] = driver.ExecuteScript(calculatedWidth),
                    ["height"] = calculatedHeight,
                    ["deviceScaleFactor"] = ScaleFactor(false),
                    ["mobile"] = driver.ExecuteScript("return typeof window.orientation !== 'undefined'")
                };
                driver.ExecuteChromeCommand("Emulation.setDeviceMetricsOverride", metrics);
            } while (calculatedHeight != prevHeight && ++numTries < maxTries);

            // LOCAL FUNCTIONS

            int CalculateDocHeight()
            {
                // Sometimes a long, sometimes double, etc
                object jsHeight = driver.ExecuteScript(
                    @"return Math.max(
                        document.body.scrollHeight,
                        document.body.offsetHeight,
                        document.documentElement.clientHeight,
                        document.documentElement.scrollHeight,
                        document.documentElement.offsetHeight,
                        document.documentElement.getBoundingClientRect().height)");
                double numericHeight = Convert.ToDouble(jsHeight);
                return (int)Math.Ceiling(numericHeight);
            }

            // False for a 1:1 pixel ratio with the image
            // True for an easier-to-read image on the monitor.
            double ScaleFactor(bool shouldScaleImage) =>
                shouldScaleImage
                ? (double)driver.ExecuteScript("return window.devicePixelRatio")
                : 1.0;
        }

        public static void ClearResize(this ChromeDriver driver)
        {
            try
            {
                driver.ExecuteChromeCommand("Emulation.clearDeviceMetricsOverride", new Dictionary<string, object>());
            }
            catch (Exception ex)
            {
                // Usually thrown when the driver or browser window is closed.
                if (!(ex is WebDriverException || ex is NoSuchWindowException))
                {
                    throw;
                }
            }
        }

        public static void HighlightElements(this ChromeDriver driver, IEnumerable<string> cssSelectors, string color = "red")
        {
            var combinedSelector = string.Join(",", cssSelectors);

            var script = $@"
var style = document.createElement('style');
style.type = 'text/css';
style.innerHTML = `{combinedSelector} {{ border: 3px dashed {color}; }}`;
document.getElementsByTagName('head')[0].appendChild(style);";

            driver.ExecuteScript(script);
        }

        public static void IncognitoMode(this ChromeOptions options)
        {
            options.AddArgument("--incognito");
        }

        /// <summary>
        /// Temporarily installs a browser driver extension, if needed, to handle basic authentication.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="extensionFilePath"></param>
        /// <param name="credentials"></param>
        /// <returns></returns>
        public static void GenerateBasicAuthenticationExtension(this ChromeOptions options, string extensionFilePath, ProjectCredentials credentials)
        {
            var extensionDir = "temp-extension-files";
            using var ext = new ChromeAuthExtension(credentials, extensionDir);
            if (File.Exists(extensionFilePath))
            {
                File.Delete(extensionFilePath);
            }
            ext.CreateZip(extensionFilePath);
            options.AddArguments("--no-sandbox");
            options.AddExtensions(extensionFilePath);
        }
    }
}