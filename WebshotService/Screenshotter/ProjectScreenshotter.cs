using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebshotService.Entities;
using WebshotService.ProjectStore;

namespace WebshotService.Screenshotter
{
    public sealed class ProjectScreenshotter
    {
        private readonly Project _project;
        private readonly IProjectStore _projectStore;
        private readonly string _sessionId;

        private ScreenshotOptions ScreenshotOptions => _project.Options.ScreenshotOptions;
        private readonly DateTime _creationTimestamp = DateTime.Now;
        private string ScreenshotDir => _projectStore.GetSessionDirectory(_sessionId);
        private readonly ILogger<ProjectScreenshotter> _logger;

        public ProjectScreenshotter(IProjectStore projectStore, ILogger<ProjectScreenshotter> logger)
        {
            _projectStore = projectStore;
            _project = projectStore.Load();
            _sessionId = projectStore.CreateSession();
            _logger = logger;
        }

        public async Task TakeScreenshotsAsync(CancellationToken? token = null, IProgress<TaskProgress>? progress = null)
        {
            var results = new SessionScreenshots();

            using ChromeDriverAdapter ss = new(ScreenshotOptions, _project.SpiderResults.BrokenLinks, _project.Options.Credentials);
            int i = 0;
            var selectedTargets = ScreenshotOptions.TargetPages
                .Where(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var uri in selectedTargets)
            {
                if (token?.IsCancellationRequested == true)
                {
                    throw new TaskCanceledException("The screenshotting task was canceled.");
                }

                progress?.Report(++i, selectedTargets.Count, uri.AbsoluteUri);

                PageScreenshots result = await ScreenshotPageAsAllDevices(ss, uri);
                results = results with { PageScreenshots = results.PageScreenshots.Add(result) };
            }

            _projectStore.SaveResults(_sessionId, results);
            progress?.Report(100, 100, "Completed Taking Screenshots");
        }

        private IEnumerable<(Device Device, int Width)> GetEnabledDeviceSizes() =>
            ScreenshotOptions.DeviceOptions
                .Where(x => x.Value.Enabled && x.Value.PixelWidth > 0)
                .Select(x => (x.Key, x.Value.PixelWidth));

        private async Task<PageScreenshots> ScreenshotPageAsAllDevices(ChromeDriverAdapter driver, Uri uri)
        {
            Dictionary<Device, Screenshot> screenshots = new();
            foreach ((Device device, int width) in GetEnabledDeviceSizes())
            {
                var path = GetImagePath(uri, device);
                screenshots[device] = await TryTakeScreenshot(driver, uri, device, width, path);
            }
            return new(uri, DateTime.Now, screenshots.ToImmutableDictionary());
        }

        private async Task<Screenshot> TryTakeScreenshot(ChromeDriverAdapter driver, Uri uri, Device device, int width, string path)
        {
            try
            {
                return await TakeScreenshot();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error taking screenshot of {0}.", uri);
                return new(null, path, ex.Message);
            }

            // HELPER FNS

            async Task<Screenshot> TakeScreenshot()
            {
                NavigationTiming PerformScreenshot() => driver.TakeScreenshot(uri.AbsoluteUri, path, width);
                NavigationTiming timing = await Task.Run(PerformScreenshot);
                return new Screenshot(timing, path, null);
            }
        }

        private string GetImagePath(Uri url, Device device)
        {
            var baseName = Utils.SanitizeFilename(url.ToString());
            var filename = $"{baseName}.{device}{ChromeDriverAdapter.ImageExtension}";
            return Path.Combine(ScreenshotDir, filename);
        }
    }

    //internal record DeviceScreenshotResult(string Filename, NavigationTiming Timing);
}