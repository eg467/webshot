﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

        public ProjectScreenshotter(IProjectStore projectStore)
        {
            _projectStore = projectStore;
            _project = projectStore.Load();
            _sessionId = projectStore.CreateSession();
        }

        public async Task TakeScreenshotsAsync(CancellationToken? token = null, IProgress<TaskProgress>? progress = null)
        {
            var results = new ScreenshotResults();

            using ChromeDriverAdapter ss = new(ScreenshotOptions, _project.SpiderResults.BrokenLinks, _project.Options.Credentials);
            int i = 0;
            var targetPages = ScreenshotOptions.TargetPages;
            foreach (var uri in targetPages)
            {
                if (token?.IsCancellationRequested == true)
                {
                    throw new TaskCanceledException("The screenshotting task was canceled.");
                }

                progress?.Report(++i, targetPages.Length, uri.AbsoluteUri);

                var result = await ScreenshotPageAsAllDevices(ss, uri);
                results = results with { Screenshots = results.Screenshots.Add(result) };
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
            PageScreenshots pageResults = new(uri);
            foreach (var size in GetEnabledDeviceSizes())
            {
                try
                {
                    DeviceScreenshotResult deviceResults = await ScreenshotPageAsDeviceAsync(driver, uri, size.Device, size.Width);
                    pageResults = pageResults with
                    {
                        PathsByDevice = pageResults.PathsByDevice.Add(size.Device, deviceResults.Filename),
                        RequestTiming = pageResults.RequestTiming ?? deviceResults.Timing
                    };
                }
                catch (Exception ex)
                {
                    pageResults = pageResults with { Error = ex.ToString() };
                    // TODO: Add Logging
                    Debug.WriteLine(ex.ToString());
                }
            }
            return pageResults;
        }

        private async Task<DeviceScreenshotResult> ScreenshotPageAsDeviceAsync(
                ChromeDriverAdapter driver,
                Uri url,
                Device device,
                int width)
        {
            var imgPath = GetImagePath();
            NavigationTiming timing = await Task.Run(TakeScreenshot);
            return new DeviceScreenshotResult(imgPath, timing);

            NavigationTiming TakeScreenshot()
            {
                return driver.TakeScreenshot(url.AbsoluteUri, imgPath, width);
            }

            string GetImagePath()
            {
                var baseName = Utils.SanitizeFilename(url.ToString());
                var filename = $"{baseName}.{device}{ChromeDriverAdapter.ImageExtension}";
                return Path.Combine(ScreenshotDir, filename);
            }
        }
    }

    internal record DeviceScreenshotResult(string Filename, NavigationTiming Timing);
}