using System;
using System.Collections.Immutable;

namespace WebshotService.Entities
{
    public record BrokenLink
    {
        public Uri Target { get; init; }
        public ImmutableHashSet<Link> Sources { get; init; }
        public string? Error { get; init; }

        public BrokenLink(Uri target, ImmutableHashSet<Link> sources, string? error = null)
        {
            Target = target;
            Sources = sources;
            Error = error;
        }
    }
}