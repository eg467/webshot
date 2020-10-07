using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;

namespace WebshotService.Entities
{
    public record CrawlResults
    {
        public ImmutableDictionary<Uri, SpiderPageStatus> Uris { get; init; }

        public ImmutableList<BrokenLink> BrokenLinks { get; init; }

        public DateTime Timestamp { get; init; }

        public CrawlResults() : this(ImmutableDictionary<Uri, SpiderPageStatus>.Empty, ImmutableList<BrokenLink>.Empty, DateTime.Now)
        {
        }

        public CrawlResults(
            ImmutableDictionary<Uri, SpiderPageStatus> uris,
            ImmutableList<BrokenLink> brokenLinks,
            DateTime timestamp)
        {
            Uris = uris;
            BrokenLinks = brokenLinks;
            Timestamp = timestamp;
        }

        public IEnumerable<Uri> SitePages => UrisByStatus(SpiderPageStatus.Visited);

        private IEnumerable<Uri> UrisByStatus(SpiderPageStatus status) =>
            Uris
            .Where(x => x.Value == status)
            .Select(x => x.Key)
            .OrderBy(x => x.ToString());
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum SpiderPageStatus
    {
        Visited, Unvisited, Excluded, Redirected, Error
    }
}