using ImageProcessor.Imaging.Formats;
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
        /// <summary>
        /// The file extension for saved images (including period).
        /// </summary>
        private const string _screenshot = ".jpeg";

        private readonly Project _project;
        private readonly IProjectStore _projectStore;
        private readonly string _sessionId;

        private ScreenshotOptions ScreenshotOptions => _project.Options.ScreenshotOptions;
        private readonly DateTime _creationTimestamp = DateTime.Now;
        private string ScreenshotDir => _projectStore.GetSessionDirectory(_sessionId);
        private readonly ILogger<ProjectScreenshotter> _logger;
        private readonly Device _deviceFilter;

        public ProjectScreenshotter(IProjectStore projectStore, ILogger<ProjectScreenshotter> logger, Device deviceFilter, string? sessionId = null)
        {
            _deviceFilter = deviceFilter;
            _projectStore = projectStore;
            _project = projectStore.Load();
            _sessionId = sessionId ?? projectStore.CreateSession();
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
                token?.ThrowIfCancellationRequested();

                progress?.Report(++i, selectedTargets.Count, uri.AbsoluteUri);

                PageScreenshots result = await ScreenshotPageAsAllDevices(ss, uri);
                results = results with { PageScreenshots = results.PageScreenshots.Add(result) };
            }

            _projectStore.SaveResults(_sessionId, results);
            progress?.Report(100, 100, "Completed Taking Screenshots");
        }

        private bool IsFiltered(Device device) => _deviceFilter.HasFlag(device);

        private IEnumerable<(Device Device, int Width)> GetEnabledDeviceSizes() =>
            ScreenshotOptions.DeviceOptions
                .Where(x => IsFiltered(x.Key) && x.Value.Enabled && x.Value.PixelWidth > 0)
                .Select(x => (x.Key, x.Value.PixelWidth));

        private async Task<PageScreenshots> ScreenshotPageAsAllDevices(ChromeDriverAdapter driver, Uri uri)
        {
            Dictionary<Device, Screenshot> screenshots = new();
            foreach ((Device device, int width) in GetEnabledDeviceSizes())
            {
                var path = GetImageFilenamePath(uri, device);
                screenshots[device] = await TryTakeScreenshot(driver, uri, device, width, path);
            }
            return new(uri, DateTime.Now, screenshots.ToImmutableDictionary());
        }

        private async Task<Screenshot> TryTakeScreenshot(ChromeDriverAdapter driver, Uri uri, Device device, int width, string imagePathWithoutExtension)
        {
            try
            {
                return await TakeScreenshot();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error taking screenshot of {0}.", uri);
                return new(null, imagePathWithoutExtension, ex.Message);
            }

            // HELPER FNS

            async Task<Screenshot> TakeScreenshot()
            {
                // Must be png due to .net core chrome driver restrictions.
                var screenshotPath = imagePathWithoutExtension + ".png";
                NavigationTiming PerformScreenshot() => driver.TakeScreenshot(uri.AbsoluteUri, screenshotPath, width);
                NavigationTiming timing = await Task.Run(PerformScreenshot);
                using var imageFactory = new ImageProcessor.ImageFactory();

                // For testing/quality comparison
                //var qualities = new int[] { 50, 60, 70, 80 };
                //foreach (var quality in qualities)
                //{
                //    imageFactory.Load(screenshotPath)
                //        .Format(new JpegFormat() { Quality = quality })
                //        .Save($"{imagePathWithoutExtension}-{quality}.jpeg");

                //    imageFactory.Load(screenshotPath)
                //        .Format(new PngFormat() { Quality = quality })
                //        .Save($"{imagePathWithoutExtension}-{quality}.png");
                //}

                var finalPath = imagePathWithoutExtension + ".jpeg";
                imageFactory.Load(screenshotPath)
                    .Format(new PngFormat() { Quality = 60 })
                    .Save(finalPath);

                //File.Delete(screenshotPath);
                return new Screenshot(timing, finalPath, null);
            }
        }

        /// <summary>
        /// Gets an image's full path without the extension.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private string GetImageFilenamePath(Uri url, Device device)
        {
            var baseName = Utils.SanitizeFilename(url.ToString());
            var filename = $"{baseName}.{device}";
            return Path.Combine(ScreenshotDir, filename);
        }
    }

    //internal record DeviceScreenshotResult(string Filename, NavigationTiming Timing);
}