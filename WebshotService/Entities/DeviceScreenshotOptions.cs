namespace WebshotService.Entities
{
    public record DeviceScreenshotOptions(Device Device = Device.Desktop, int PixelWidth = 1920, bool Enabled = true);
}