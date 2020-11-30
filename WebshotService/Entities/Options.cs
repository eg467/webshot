using System;

namespace WebshotService.Entities
{
    public record Options
    {
        public SpiderOptions SpiderOptions { get; init; } = new();
        public ScreenshotOptions ScreenshotOptions { get; init; } = new();
        public ProjectCredentials Credentials { get; init; } = new();
        public string[] LighthouseTests { get; set; } = Lighthouse.Lighthouse.IsInstalled
            ? new[] { Lighthouse.Lighthouse.Categories.Performance }
            : Array.Empty<string>();
    }
}