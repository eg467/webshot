using System.IO;

namespace WebshotService.Entities
{
    public record DeviceScreenshotFile(Screenshot Result, Device Device);
}