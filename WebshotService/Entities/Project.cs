using System;

namespace WebshotService.Entities
{
    public record Project
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "Webshot Project";
        public DateTime Created { get; init; } = DateTime.Now;
        public Options Options { get; init; } = new();
        public CrawlResults SpiderResults { get; init; } = new();
    }
}