using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebshotService.Entities;

namespace WebshotService.Spider
{
    public sealed class Spider : IDisposable
    {
        private readonly LinkTracker _linkTracker = new LinkTracker();
        private readonly SpiderOptions _options;
        private readonly List<IUriCrawlValidator> _uriCrawlValidators = new List<IUriCrawlValidator>();
        private readonly ILogger _logger;

        public Spider(SpiderOptions options, ProjectCredentials creds, ILogger logger)
        {
            _options = options;
            _logger = logger;

            SetHttpClientCredentials(creds);
            ConfigureUriCrawlValidators();
        }

        private static void SetHttpClientCredentials(ProjectCredentials projectCreds)
        {
            projectCreds?.CredentialsByDomain?.ForEach(c =>
            {
                string user = c.Value.DecryptUser();
                string pass = c.Value.DecryptPassword();
                NetworkCredential creds = new(user, pass);
                WebshotHttpClient.AddCredential(c.Key, creds);
            });
        }

        private void ConfigureUriCrawlValidators()
        {
            _uriCrawlValidators.Clear();
            Action<IUriCrawlValidator> Add = _uriCrawlValidators.Add;
            if (!_options.FollowExternalLinks)
            {
                Add(new UriInTrackedHostValidator(_options.SeedUris));
            }

            if (!string.IsNullOrEmpty(_options.UriBlacklistPattern))
            {
                Add(new UriRegexValidator(_options.UriBlacklistPattern, UriCrawlValidatorType.Blacklist));
            }

            Add(new UriRecursionValidator());
            Add(new PermittedUriSchemeValidator("http", "https"));
            Add(new ForbiddenUriExtensionValidator("css", "png", "jpg", "jpeg", "js", "pdf"));
        }

        public Task<CrawlResults> Crawl(IProgress<TaskProgress>? progress = null) =>
            Crawl(CancellationToken.None, progress);

        public async Task<CrawlResults> Crawl(
            CancellationToken token,
            IProgress<TaskProgress>? progress = null)
        {
            _linkTracker.Clear();
            _options.SeedUris.Select(x => new Link(x, "")).ForEach(FoundLink);

            while (_linkTracker.TryNextUnvisited(out StandardizedUri? unvisited))
            {
                _logger.LogDebug("Parsing {0}", unvisited!.Standardized.AbsoluteUri);

                if (token.IsCancellationRequested)
                    throw new TaskCanceledException();

                var currentProgress = new TaskProgress(
                    _linkTracker.Count(u => u.Status != SpiderPageStatus.Unvisited),
                    _linkTracker.Count(),
                    unvisited.Standardized.ToString());

                progress?.Report(currentProgress);

                await Visit(unvisited);
            }

            TaskProgress completionProgress = new TaskProgress(_linkTracker.Count(), _linkTracker.Count(), "Complete");
            progress?.Report(completionProgress);

            return _linkTracker.ToCrawlResults();
        }

        private async Task Visit(StandardizedUri uri)
        {
            UriSources sources = _linkTracker.GetOrCreateSources(uri);
            var html = "";
            try
            {
                (Uri finalUri, string content) = await WebshotHttpClient.DownloadPageAsync(uri.Standardized);
                html = content;
                sources = _linkTracker.CombineSourcesIfRedirection(uri, finalUri);
            }
            catch (HttpRequestException ex)
            {
                sources.Status = SpiderPageStatus.Error;
                sources.Error = ex.Message;
                _logger.LogWarning(ex, "Error downloading page ({0}): {1}", uri.Standardized, ex.Message);
            }

            if (sources.Status != SpiderPageStatus.Unvisited) return;

            if (!IsHtml(html))
            {
                sources.Status = SpiderPageStatus.Excluded;
                return;
            }

            sources.Status = SpiderPageStatus.Visited;

            if (_options.FollowInternalLinks)
            {
                var foundLinks = ParseLinks(sources.Uri.Uri, html).ToList();
                foreach (var link in foundLinks)
                {
                    FoundLink(link);
                }
            }
        }

        private static IEnumerable<Link> ParseLinks(Uri callingPage, string html) =>
            Regex.Matches(html, @"<a[^>]+href=""?([^""\s>]+)""?", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(m => new Link(callingPage, m!.Groups[1].Value));

        private bool ShouldVisit(Uri uri)
        {
            try
            {
                return ValidateUri(uri);
            }
            catch (UriFormatException ex)
            {
                _logger.LogWarning(ex, "Error parsing URI ({0}): {1}", uri.AbsoluteUri, ex.Message);
                return false;
            }
        }

        private bool ValidateUri(Uri uri) =>
            !string.IsNullOrEmpty(uri?.AbsoluteUri)
            && _uriCrawlValidators.All(v => v.Validate(uri));

        private static bool IsHtml(string content) =>
            content.Contains("<html", StringComparison.OrdinalIgnoreCase);

        private void FoundLink(Link link)
        {
            try
            {
                UriSources sources = _linkTracker.GetOrCreateSources(link.Target);
                sources.CallingLinks.Add(link);
                if (sources.Status != SpiderPageStatus.Excluded
                    && !ShouldVisit(sources.Uri.Standardized))
                {
                    sources.Status = SpiderPageStatus.Excluded;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing link found from webpage {link.CallingPage}->{link.Target}.");
            }
        }

        public void Dispose()
        {
            WebshotHttpClient.ClearCredentials();
        }
    }
}