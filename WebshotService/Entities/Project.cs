using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebshotService.Entities
{
    public record Project
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string Name { get; init; } = "Webshot Project";
        public DateTime Created { get; init; } = DateTime.Now;
        public Options Options { get; init; } = new Options();
        public CrawlResults SpiderResults { get; init; } = new CrawlResults();
    }
}