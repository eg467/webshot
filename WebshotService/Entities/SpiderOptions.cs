using System;
using System.Collections.Immutable;

namespace WebshotService.Entities
{
    public record SpiderOptions
    {
        public bool FollowInternalLinks { get; init; } = true;
        public bool FollowExternalLinks { get; init; }
        public string UriBlacklistPattern { get; init; } = "";
        public ImmutableArray<Uri> SeedUris { get; init; } = ImmutableArray<Uri>.Empty;
    }
}