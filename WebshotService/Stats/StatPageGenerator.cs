using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using WebshotService.Entities;
using WebshotService.ProjectStore;

namespace WebshotService.Stats
{
    public static class StatsPageGenerator
    {
        public const string DefaultFilename = "WebPagePerformance.html";

        /// <summary>
        ///
        /// </summary>
        /// <returns>The path of the saved file</returns>
        public static void SaveStatsPage(IProjectStoreFactory storeFactory, IEnumerable<string> projectIds, string outputPath)
        {
            var stats = LoadAggregateStats(storeFactory, projectIds);
            string serializedStats = JsonConvert.SerializeObject(stats);
            string template = File.ReadAllText("StatPageHtmlTemplate.html");
            string finalHtml = template.Replace("/***allData***/", $@"let allData=({serializedStats});");

            // Save to a dummy file first in case access to the main first one is opened/blocked.
            var safePath = $"{outputPath}.DONOTOPEN";
            File.WriteAllText(safePath, finalHtml, Encoding.UTF8);

            // Now write to a viewable path/extension that can fail.
            File.WriteAllText(outputPath, finalHtml, Encoding.UTF8);
        }

        private static AggregateStatistics<RequestStatistics> LoadAggregateStats(IProjectStoreFactory storeFactory, IEnumerable<string> projectIds)
        {
            AggregateStatistics<RequestStatistics> output = new();
            var stores = projectIds.Select(storeFactory.Create).Where(s => s.Exists);
            foreach (IProjectStore store in stores)
            {
                ProjectStatistics<RequestStatistics> projectStats = CreateProjectStatsFor(store);

                foreach (SessionScreenshots session in SessionScreenshotsFrom(store))
                    foreach (PageScreenshots pageScreenshots in session.PageScreenshots)
                        GetStatsByUri(projectStats, session.Timestamp, pageScreenshots);
            }

            output.Projects.ForEach(Sort);

            return output;

            // HELPER FUNCTIONS
            static IEnumerable<SessionScreenshots> SessionScreenshotsFrom(IProjectStore store)
            {
                var results = store.GetResultsBySessionId();
                var sessionScreenshots = results.Select(x => x.Result);
                return sessionScreenshots;
            }

            ProjectStatistics<RequestStatistics> CreateProjectStatsFor(IProjectStore store)
            {
                var project = store.Load();
                var projectStats = new ProjectStatistics<RequestStatistics>(project.Name, project.Id);
                output.Projects.Add(projectStats);
                return projectStats;
            }

            static RequestStatistics TimingsFrom(PageScreenshots pageScreenshots)
            {
                // Use these in order of precedence as the canonical timing for the page.
                var canonicalDeviceOrder = new[] { Device.Desktop, Device.Tablet, Device.Mobile };
                IReadOnlyDictionary<Device, Screenshot>? ss = pageScreenshots.DeviceScreenshots;
                if (ss is null)
                    return new(pageScreenshots.Timestamp, null);

                NavigationTiming? timing = canonicalDeviceOrder
                    .Where(ss.ContainsKey)
                    .Select(x => ss[x].RequestTiming)
                    .Where(x => x is object)
                    .FirstOrDefault();
                return new(pageScreenshots.Timestamp, timing);
            }

            void GetStatsByUri(ProjectStatistics<RequestStatistics> projectStats, DateTime timestamp, PageScreenshots pageScreenshots)
            {
                var uri = pageScreenshots.Uri;
                var siteStats = projectStats.SiteStatistics.FindOrAdd(
                    x => x.Uri.Equals(uri),
                    () => new SiteStatistics<RequestStatistics>(uri));

                var requestStats = TimingsFrom(pageScreenshots);
                siteStats.RequestStats.Add(requestStats);
            }

            static void Sort(ProjectStatistics<RequestStatistics> projectStats)
            {
                // Sort by URI
                Comparison<SiteStatistics<RequestStatistics>> uriComparison = (a, b) => a.Uri.AbsoluteUri.CompareTo(b.Uri.AbsoluteUri);
                projectStats.SiteStatistics.Sort(uriComparison);

                // Sort all session results by timestamp
                Comparison<RequestStatistics> dateComparison = (a, b) => a.Timestamp.CompareTo(b.Timestamp);
                foreach (var siteStats in projectStats.SiteStatistics)
                    siteStats.RequestStats.Sort(dateComparison);
            }
        }
    }

    /// <summary>
    /// All tracked projects' page request statistics.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AggregateStatistics<T>
    {
        public List<ProjectStatistics<T>> Projects { get; set; } = new List<ProjectStatistics<T>>();
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class GoogleFormattedChartData : AggregateStatistics<dynamic[]>
    {
        public string[] ColumnHeaders { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Statistics for all site-wide session runs for a project.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ProjectStatistics<T>
    {
        public string ProjectName { get; set; }
        public string ProjectId { get; set; }

        public ProjectStatistics(string projectName, string projectId)
        {
            ProjectName = projectName;
            ProjectId = projectId;
        }

        public List<SiteStatistics<T>> SiteStatistics { get; set; } = new List<SiteStatistics<T>>();
    }

    /// <summary>
    /// Request stats for a URI over time.
    /// </summary>
    /// <typeparam name="T">some representation of the page request data</typeparam>
    public class SiteStatistics<T>
    {
        public Uri Uri { get; set; }
        public List<T> RequestStats { get; set; } = new List<T>();

        public SiteStatistics(Uri uri)
        {
            Uri = uri;
        }
    }

    public class RequestStatistics
    {
        public DateTime Timestamp { get; set; }
        public NavigationTiming Timing { get; set; } = new NavigationTiming();

        public RequestStatistics()
        {
        }

        public RequestStatistics(DateTime timestamp, NavigationTiming? timing)
        {
            Timestamp = timestamp;
            Timing = timing;
        }
    }
}