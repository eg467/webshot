using System;

namespace WebshotService.Entities
{
    public record Link
    {
        /// <summary>
        /// The page that references the link.
        /// </summary>
        public Uri CallingPage { get; init; }

        /// <summary>
        /// The raw link in the anchor tag for finding the link sources.
        /// </summary>
        public string Href { get; init; }

        public Uri Target => new(CallingPage, Href);

        /// <summary>
        ///
        /// </summary>
        /// <param name="callingPage">The page that is linking to the link</param>
        /// <param name="href">The raw anchor href</param>
        public Link(Uri callingPage, string href)
        {
            CallingPage = callingPage;
            Href = href;
        }

        // Overriding record's Object.Equals(object other) throws a compiler error in c#9
        bool IEquatable<Link>.Equals(Link? other)
        {
            return CallingPage.Equals(other?.CallingPage)
                && string.Equals(Href, other.Href, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() => HashCode.Combine(CallingPage, Href.ToUpper());
    }
}