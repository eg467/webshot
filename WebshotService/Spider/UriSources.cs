using System;
using System.Collections.Generic;
using WebshotService.Entities;

namespace WebshotService.Spider
{
    public class UriSources
    {
        public SpiderPageStatus Status { get; set; } = SpiderPageStatus.Unvisited;
        public StandardizedUri Uri { get; set; }
        public Uri? RedirectedUri { get; set; }

        public string? Error { get; set; }
        public HashSet<Link> CallingLinks { get; set; } = new();

        public UriSources(StandardizedUri uri)
        {
            this.Uri = uri;
        }
    }
}