using Newtonsoft.Json.Converters;
using System;
using System.Text.Json.Serialization;

namespace WebshotService.Entities
{
    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Device
    {
        None = 0,
        Desktop = 1 << 0,
        Mobile = 1 << 1,
        Tablet = 1 << 2,
    }
}