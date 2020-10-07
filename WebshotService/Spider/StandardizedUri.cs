using System;

namespace WebshotService.Spider
{
    public class StandardizedUri
    {
        public Uri Uri { get; }
        public Uri Standardized { get; }

        public StandardizedUri(Uri uri)
        {
            Uri = uri;
            Standardized = uri.TryStandardize();
        }

        public override bool Equals(object? other)
        {
            string? absoluteUri = other switch
            {
                StandardizedUri stdUri => stdUri.Standardized.AbsoluteUri,
                string str => str,
                Uri uri => uri.AbsoluteUri,
                _ => null,
            };
            return Standardized.AbsoluteUri.Equals(absoluteUri, StringComparison.Ordinal);
        }

        public override int GetHashCode() => Standardized.GetHashCode();
    }
}
