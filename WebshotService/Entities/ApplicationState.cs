﻿using System.Collections.Generic;
using System.Collections.Immutable;

namespace WebshotService.Entities
{
    public record ApplicationState
    {
        public Project? CurrentProject { get; init; }
        public ImmutableArray<(string, SessionScreenshots)> ProjectResults { get; init; } =
            ImmutableArray<(string, SessionScreenshots)>.Empty;

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