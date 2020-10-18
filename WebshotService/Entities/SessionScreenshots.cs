using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebshotService.Entities
{
    public record SessionScreenshots
    {
        public ImmutableList<PageScreenshots> PageScreenshots { get; init; } = ImmutableList<PageScreenshots>.Empty;
        public DateTime Timestamp { get; init; }

        public SessionScreenshots(DateTime timestamp)
        {
            Timestamp = timestamp;
        }

        public SessionScreenshots() : this(DateTime.Now)
        {
        }

        public override string ToString() =>
            $"Screenshots from {Timestamp.ToLongTimeString()}";
    }
}