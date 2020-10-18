namespace WebshotService.Entities
{
    public record Screenshot(NavigationTiming? RequestTiming, string Path, string? Error);
}