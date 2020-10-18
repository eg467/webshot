using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;

namespace WebshotService.Entities
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Device
    {
        Desktop, Mobile, Tablet,
    }
}