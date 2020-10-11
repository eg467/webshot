namespace WebshotService.Entities
{
    public record Options
    {
        public SpiderOptions SpiderOptions { get; init; } = new();
        public ScreenshotOptions ScreenshotOptions { get; init; } = new();
        public ProjectCredentials Credentials { get; init; } = new();
    }
}