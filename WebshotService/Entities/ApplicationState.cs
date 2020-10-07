using System.Collections.Generic;
using System.Collections.Immutable;

namespace WebshotService.Entities
{
    public static class InitialApplicationState
    {
        public static ApplicationState FromFile(string path) =>
            new FileStore<ApplicationState>(path).Load();

        public static ApplicationState Default() => new ApplicationState();
    }

    public record ApplicationState
    {
        public Project? CurrentProject { get; init; }
        public ImmutableArray<(string, ScreenshotResults)> ProjectResults { get; init; } =
            ImmutableArray<(string, ScreenshotResults)>.Empty;

        public bool IsTakingScreenshots { get; init; }
        public bool IsCrawlingSite { get; init; }

        public TaskProgress? Progress { get; init; }

        public SchedulerState SchedulerState { get; init; } = new SchedulerState();

        /// <summary>
        /// A list of recently opened projects [Project Key, Project Label Description].
        /// </summary>
        public ImmutableArray<KeyValuePair<string, string>> RecentProjects { get; init; } =
            ImmutableArray.Create<KeyValuePair<string, string>>();
    }
}