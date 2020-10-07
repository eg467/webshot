using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebshotService.Entities
{
    public record SchedulerState
    {
        public Project? CurrentProject { get; init; }
        public ImmutableArray<ScheduledProject> ScheduledProjects { get; init; }
        public bool Enabled { get; init; }

        public SchedulerState() : this(ImmutableArray.Create<ScheduledProject>(), false)
        {
        }

        public SchedulerState(
            ImmutableArray<ScheduledProject> scheduledProjects,
            bool enabled
        )
        {
            ScheduledProjects = scheduledProjects;
            Enabled = enabled;
        }

        public SchedulerState(
            ImmutableArray<ScheduledProject> scheduledProjects,
            bool enabled,
            Project currentProject
        ) : this(scheduledProjects, enabled)
        {
            CurrentProject = currentProject;
        }
    }

    public record ScheduledProject
    {
        /// <summary>
        /// The file path or identifier for the project.
        /// </summary>
        public string ProjectId { get; init; }

        /// <summary>
        /// The user-chosen friendly identifier for the project
        /// </summary>
        public string ProjectName { get; init; } = "";

        /// <summary>
        /// A comma-separated list of domains of which screenshots will be taken.
        /// </summary>
        public string TargetDomains { get; init; } = "";

        public bool Enabled { get; init; } = true;

        public DateTime? LastRun { get; init; }
        public bool RunImmediately { get; init; }
        public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(60);

        public DateTime? ScheduledFor
        {
            get
            {
                if (RunImmediately) return DateTime.Now;
                if (!Enabled) return null;
                if (!LastRun.HasValue) return DateTime.Now;
                return LastRun.Value.Add(Interval);
            }
        }

        public ScheduledProject(
            string projectId,
            string projectName,
            string targetDomains,
            bool enabled,
            DateTime? lastRun,
            bool runImmediately,
            TimeSpan interval)
        {
            ProjectId = projectId;
            ProjectName = projectName ?? "";
            TargetDomains = targetDomains ?? "";
            Enabled = enabled;
            LastRun = lastRun;
            RunImmediately = runImmediately;
            Interval = interval;
        }
    }
}