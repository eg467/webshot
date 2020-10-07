using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebshotService.Entities
{
    public record ScreenshotOptions
    {
        public ImmutableArray<Uri> TargetPages { get; init; } = ImmutableArray<Uri>.Empty;
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