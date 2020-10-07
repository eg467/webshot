using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using WebshotService.Entities;

namespace WebshotService.Spider
{
    public class LinkTracker
    {
        private readonly Dictionary<StandardizedUri, UriSources> _uris =
            new Dictionary<StandardizedUri, UriSources>();

        public int Count(Func<UriSources, bool>? predicate = null) =>
            predicate != null ? _uris.Count(x => predicate(x.Value)) : _uris.Count;

        public void Clear() => _uris.Clear();

        public CrawlResults ToCrawlResults() => new CrawlResults(
            ByStatus.ToImmutableDictionary(),
            BrokenLinks.ToImmutableList(),
            DateTime.Now);

        public List<BrokenLink> BrokenLinks =>
            _uris
            .Where(x => x.Value.Status == SpiderPageStatus.Error)
            .Select(x => new BrokenLink(
                x.Key.Standardized,
                x.Value.CallingLinks.ToImmutableHashSet(),
                x.Value.Error))
            .ToList();

        public Dictionary<Uri, SpiderPageStatus> ByStatus =>
            _uris
            .ToDictionary(x => x.Key.Standardized, x => x.Value.Status);

        public UriSources GetOrCreateSources(Uri uri) => GetOrCreateSources(new StandardizedUri(uri));

        public UriSources GetOrCreateSources(StandardizedUri uri)
        {
            if (!_uris.TryGetValue(uri, out var sources))
            {
                sources = new UriSources(uri);
                _uris[uri] = sources;
            }
            return sources;
        }

        public UriSources CombineSourcesIfRedirection(StandardizedUri sourceUri, Uri redirectionTarget)
        {
            var src = GetOrCreateSources(sourceUri);
            src.RedirectedUri = redirectionTarget;

            StandardizedUri standardRedirectionTarget = new(redirectionTarget);
            if (sourceUri.Equals(standardRedirectionTarget))
            {
                // Not a meaningful redirection
                return src;
            }

            // The request has been redirected,
            // so the source and destination pages should be considered equivalent.
            // Combine the pages that point to either.
            src.Status = SpiderPageStatus.Redirected;

            var dest = GetOrCreateSources(standardRedirectionTarget);
            var combinedLinks = src.CallingLinks.Union(dest.CallingLinks).ToHashSet();
            src.CallingLinks = combinedLinks;
            dest.CallingLinks = combinedLinks;

            return dest;
        }

        public bool TryNextUnvisited(out StandardizedUri? uri)
        {
            uri = _uris
                .Where(x => x.Value.Status == SpiderPageStatus.Unvisited)
                .Select(x => x.Key)
                .FirstOrDefault();
            return uri is object;
        }
    }
}