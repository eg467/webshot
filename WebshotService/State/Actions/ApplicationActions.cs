using Redux;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using WebshotService.Entities;

namespace WebshotService.State.Actions.ApplicationActions
{
    public record SetApplication(ApplicationState ApplicationState) : IAction;
    public record SetProgress(TaskProgress? Progress) : IAction;
    public record SetIsTakingScreenshots(bool IsTakingScreenshots) : IAction;
    public record SetIsCrawlingSite(bool IsCrawling) : IAction;
}

namespace WebshotService.State.Actions.ProjectActions
{
    public record SetProject(Project? Project) : IAction;
    public record SetProjectResults(IImmutableList<(string, SessionScreenshots)> Results) : IAction;
    public record AddProjectResult(string Id, SessionScreenshots Results) : IAction;
    public record Rename(string Name) : IAction;
    public record SetOptions(Options Options) : IAction;
    public record SetSpiderOptions(SpiderOptions Options) : IAction;
    public record SetScreenshotOptions(ScreenshotOptions Options) : IAction;
    public record SetProjectCredentials(ProjectCredentials Credentials) : IAction;
    public record SetSeedUris(IEnumerable<Uri> Uris) : IAction;
    public record SetTargetPages(IReadOnlyDictionary<Uri, bool> Pages) : IAction;
    public record ToggleTargetPageEnabled(Uri Page, bool Enabled) : IAction;
    public record SetCrawlResults(CrawlResults Results) : IAction;
}

namespace WebshotService.State.Actions.SchedulerActions
{
    public record ToggleScheduler(bool Enabled) : IAction;
    public record SetSchedulerOptions(
        List<ScheduledProject> Projects,
        bool Enabled,
        string CurrentProjectId,
        Project CurrentProject) : IAction;
    public record AddScheduledProject(ScheduledProject NewScheduledProject) : IAction;
    public record RemoveProject(string ProjectId) : IAction;
    public record UpdateScheduledProject(ScheduledProject Project) : IAction;
    public record RunImmediately(string ProjectId) : IAction;
    public record MarkScheduledProjectComplete : IAction { }
    public record CurrentProjectChanged(Project? Project) : IAction;
}

namespace WebshotService.State.Actions.RecentProjectsActions
{
    public record Set(List<KeyValuePair<string, string>> History) : IAction;
    public record Add(string Id, string Description) : IAction;
    public record Clear : IAction { }
}