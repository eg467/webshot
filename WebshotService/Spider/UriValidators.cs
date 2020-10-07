using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebshotService.Spider
{

    /// <summary>
    /// Determines if a url should be validated.
    /// </summary>
    internal interface IUriCrawlValidator
    {
        public bool Validate(Uri uri);
    }

    internal enum UriCrawlValidatorType { Whitelist, Blacklist }

    internal class UriInTrackedHostValidator : IUriCrawlValidator
    {
        private readonly IEnumerable<Uri> _allowedUris;

        public UriInTrackedHostValidator(IEnumerable<Uri> allowedUris)
        {
            _allowedUris = allowedUris;
        }

        public bool Validate(Uri uri)
        {
            bool MatchesHost(Uri allowedHost) =>
                string.Equals(uri.Host, allowedHost.Host, StringComparison.OrdinalIgnoreCase);
            return !uri.IsAbsoluteUri || _allowedUris.Any(MatchesHost);
        }
    }

    /// <summary>
    /// Check if last N path segments repeat
    /// because WordPress can generate infinite recursion with poorly formed links.
    /// e.g. https://example.com/pageslug/pageslug/pageslug/.../
    /// </summary>
    internal class UriRecursionValidator : IUriCrawlValidator
    {
        private readonly int _maxRecursionLevel;

        public UriRecursionValidator(int maxRecursionLevel = 2)
        {
            _maxRecursionLevel = maxRecursionLevel;
        }

        public bool Validate(Uri uri)
        {
            var recursionCheckDesired = _maxRecursionLevel >= 1;
            var illegalRecursionPossible = uri.Segments.Length > _maxRecursionLevel + 1;

            var hasRecursed =
                recursionCheckDesired
                && illegalRecursionPossible
                && uri.Segments
                    .Skip(uri.Segments.Length - _maxRecursionLevel)
                    .Select(s => s.TrimEnd('/'))
                    .Unanimous();
            return !hasRecursed;
        }
    }



    internal class UriRegexValidator : IUriCrawlValidator
    {
        private readonly string _pattern;
        private readonly bool _caseSensitive;

        /// <summary>
        /// True to include the matched pattern, false to reject a matching pattern.
        /// </summary>
        private readonly UriCrawlValidatorType _isWhitelist;

        public UriRegexValidator(string pattern, UriCrawlValidatorType isWhitelist = UriCrawlValidatorType.Whitelist, bool caseSensitive = false)
        {
            _pattern = pattern;
            _caseSensitive = caseSensitive;
            _isWhitelist = isWhitelist;
        }

        public bool Validate(Uri uri)
        {
            var options = _caseSensitive ? RegexOptions.IgnoreCase : RegexOptions.None;
            var isMatch = Regex.IsMatch(uri.AbsoluteUri, _pattern, options);
            return _isWhitelist == UriCrawlValidatorType.Whitelist ? isMatch : !isMatch;
        }
    }

    internal class ForbiddenUriExtensionValidator : UriRegexValidator
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="extensionBlacklist">Forbidden URI extensions, not including periods.</param>
        public ForbiddenUriExtensionValidator(params string[] extensionBlacklist)
            : base($@"\.({string.Join("|", extensionBlacklist)})\b", UriCrawlValidatorType.Blacklist)
        {
        }
    }

    internal class PermittedUriSchemeValidator : UriRegexValidator
    {
        public PermittedUriSchemeValidator(params string[] schemeWhitelist)
            : base($@"^{string.Join("|", schemeWhitelist)}:", UriCrawlValidatorType.Whitelist)
        {
        }
    }
}
