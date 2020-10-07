using System;
using System.Collections.Immutable;

namespace WebshotService.Entities
{
    public record ScreenshotOptions
    {
        public ImmutableDictionary<Uri, bool> TargetPages { get; init; } = ImmutableDictionary<Uri, bool>.Empty;
        public ImmutableDictionary<Device, DeviceScreenshotOptions> DeviceOptions { get; init; }
        public bool OverwriteResults { get; init; }
        public bool HighlightBrokenLinks { get; init; }

        public ScreenshotOptions()
        {
            var builder = ImmutableDictionary.CreateBuilder<Device, DeviceScreenshotOptions>();
            builder.Add(Device.Desktop, new DeviceScreenshotOptions(Device.Desktop, 1920));
            builder.Add(Device.Desktop, new DeviceScreenshotOptions(Device.Mobile, 480));
            builder.Add(Device.Desktop, new DeviceScreenshotOptions(Device.Tablet, 768));
            DeviceOptions = builder.ToImmutable();
        }
    }
}