using System;
using System.IO;

namespace WebshotService.Entities
{
    public record DeviceScreenshotFile(Device Device, PageScreenshots Result);

    public static class DeviceScreenshotFileExtensions
    {
        public static string GetPath(this DeviceScreenshotFile screenshotFile, string basePath) =>
            Path.Combine(basePath, screenshotFile.Result.PathsByDevice[screenshotFile.Device]);
    }
}