using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using WebshotService.Stats;

namespace WebshotService.Entities
{
    public record PageScreenshots(Uri Uri, DateTime Timestamp, ImmutableDictionary<Device, Screenshot> DeviceScreenshots);

    public static class PageScreenshotsExtensions
    {
        public static RequestStatistics PageStats(this PageScreenshots pageScreenshots)
        {
            // Use these in order of precedence as the canonical timing for the page.
            var canonicalDeviceOrder = new[] { Device.Desktop, Device.Tablet, Device.Mobile };
            IReadOnlyDictionary<Device, Screenshot>? ss = pageScreenshots.DeviceScreenshots;
            if (ss is null)
                return new(pageScreenshots.Timestamp, null);

            NavigationTiming? timing = canonicalDeviceOrder
                .Where(ss.ContainsKey)
                .Select(x => ss[x].RequestTiming)
                .Where(x => x is object)
                .FirstOrDefault();
            return new(pageScreenshots.Timestamp, timing);
        }
    }
}