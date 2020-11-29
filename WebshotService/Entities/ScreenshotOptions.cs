using System;
using System.Collections.Immutable;

namespace WebshotService.Entities
{
    public record ScreenshotOptions
    {
        public ImmutableDictionary<Uri, bool> TargetPages { get; init; } = ImmutableDictionary<Uri, bool>.Empty;
        public ImmutableDictionary<Device, DeviceScreenshotOptions> DeviceOptions { get; init; }
        public bool OverwriteResults { get; init; }
        public bool HighlightBrokenLinks { get; init; } = true;

        public ScreenshotOptions()
        {
            var builder = ImmutableDictionary.CreateBuilder<Device, DeviceScreenshotOptions>();
            builder.Add(Device.Desktop, new DeviceScreenshotOptions(Device.Desktop, 1920, Enabled: true));
            builder.Add(Device.Mobile, new DeviceScreenshotOptions(Device.Mobile, 480, Enabled: false));
            builder.Add(Device.Tablet, new DeviceScreenshotOptions(Device.Tablet, 768, Enabled: false));
            DeviceOptions = builder.ToImmutable();
        }
    }
}