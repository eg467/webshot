using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

namespace WebshotService.Spider
{
    /// <summary>
    /// A single <see cref="HttpClient"/> reference with support for basic authentication.
    /// </summary>
    internal static class WebshotHttpClient
    {
        private static readonly HttpClientHandler HttpClientHandler = new HttpClientHandler();
        public static readonly HttpClient Client = new HttpClient(HttpClientHandler);
        private static readonly CredentialCache _credentialCache = new CredentialCache();

        /// <summary>
        /// Maintains a list of URIs with tracked credentials to facilitate later removal.
        /// </summary>
        private static readonly List<Uri> _urisWithCredentials = new();

        private const string AuthType = "Basic";

        static WebshotHttpClient()
        {
            HttpClientHandler.Credentials = _credentialCache;
            SetUserAgentHeader();
        }

        private static void SetUserAgentHeader()
        {
            // Identify as a bot user agent
            var assembly = Assembly.GetAssembly(typeof(WebshotHttpClient));
            var version = assembly?.GetName().Version ?? new Version(1, 0);
            var userAgentName = nameof(WebshotService);
            var userAgentVersion = version.ToString();
            ProductInfoHeaderValue userAgentHeader = new(userAgentName, userAgentVersion);
            Client.DefaultRequestHeaders.UserAgent.Add(userAgentHeader);
        }

        public static void AddCredential(Uri host, NetworkCredential credential)
        {
            if (_credentialCache.GetCredential(host, AuthType) is object) return;
            _urisWithCredentials.Add(host);
            _credentialCache.Add(host, AuthType, credential);
        }

        public static void ClearCredentials()
        {
            static void RemoveFromCache(Uri u) => _credentialCache.Remove(u, AuthType);
            _urisWithCredentials.ForEach(RemoveFromCache);
            _urisWithCredentials.Clear();
        }

        /// <summary>
        /// Downloads the contents of a web page
        /// </summary>
        /// <param name="uri">The URI of the page to download</param>
        /// <returns>A tuple of the Uri of the final page (after redirection(s)), and its HTML contents.</returns>
        public static async Task<(Uri finalPage, string contents)> DownloadPageAsync(Uri uri)
        {
            using HttpResponseMessage response = await Client.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            using HttpContent content = response.Content;
            if (response.RequestMessage is null)
            {
                throw new HttpRequestException("The HTTP response is empty.");
            }
            Uri finalPage = response.RequestMessage.RequestUri ?? uri;
            string contents = await content.ReadAsStringAsync();
            return (finalPage, contents);
        }
    }
}