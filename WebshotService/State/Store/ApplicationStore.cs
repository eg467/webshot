using Redux;
using System.Reactive.Linq;
using System;
using WebshotService.Entities;
using WebshotService.ProjectStore;
using System.Threading.Tasks;
using System.Collections.Generic;
using WebshotService.State.Actions;
using System.Threading;
using System.Linq;
using WebshotService.Screenshotter;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;

namespace WebshotService.State.Store
{
    /// <summary>
    /// Facade for the redux store and screenshotting tools. Effectively automatially dispatched Redux action creators.
    /// </summary>
    public sealed class ApplicationStore : IDisposable, IObservable<ApplicationState>
    {
        public ApplicationState State => _store.GetState();

        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IStore<ApplicationState> _store;
        private readonly IProjectStoreFactory _projectStoreFactory;

        public ApplicationStore(IStore<ApplicationState> store, IProjectStoreFactory projectStoreFactory, ILoggerFactory loggerFactory)
        {
            _store = store;
            _projectStoreFactory = projectStoreFactory;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<ApplicationStore>();
        }

        public void SetState(ApplicationState state)
        {
            _store.Dispatch(new Actions.ApplicationActions.SetApplication(state));
        }

        public void SetSeedUris(IEnumerable<Uri> uris)
        {
            _store.Dispatch(new Actions.ProjectActions.SetSeedUris(uris));
        }

        public void SetCredentials(ProjectCredentials credentials)
        {
            _store.Dispatch(new Actions.ProjectActions.SetProjectCredentials(credentials));
        }

        public void SetSpiderOptions(SpiderOptions spiderOptions)
        {
            _store.Dispatch(new Actions.ProjectActions.SetSpiderOptions(spiderOptions));
        }

        public void SetSpiderWorking(bool status)
        {
            _store.Dispatch(new Actions.ApplicationActions.SetIsCrawlingSite(status));
        }

        public void SetCurrentProject(string id)
        {
            IProjectStore projectStore = _projectStoreFactory.Create(id);
            _store.Dispatch(ActionCreators.LoadProject(projectStore));
        }

        public void CloseCurrentProject()
        {
            _store.Dispatch(ActionCreators.CloseProject());
        }

        public void DeleteProject(string id)
        {
            if (string.Equals(State.CurrentProject?.Id, id))
                CloseCurrentProject();

            var newList = State.RecentProjects.ToList();
            newList.RemoveAll(x => x.Key == id);
            _store.Dispatch(new Actions.RecentProjectsActions.Set(newList));
        }

        public void SetProgress(TaskProgress currentProgress)
        {
            _store.Dispatch(new Actions.ApplicationActions.SetProgress(currentProgress));
        }

        public void SetLighthouseTests(List<string> tests)
        {
            if (tests.Except(Lighthouse.Lighthouse.Categories.GetAll()).Any())
            {
                throw new ArgumentException("Invalid Lighthouse test(s) provided.", nameof(tests));
            }

            _store.Dispatch(new Actions.ProjectActions.SetLighthouseTests(tests));
        }

        public async Task<string?> RunLighthouseTests(
            string? sessionId = null,
            CancellationToken? token = null,
            IProgress<TaskProgress>? progress = null)
        {
            if (State.CurrentProject is null)
            {
                throw new InvalidOperationException("No project is opened.");
            }
            var selectedTests = State.CurrentProject.Options.LighthouseTests;
            if (selectedTests.Except(Lighthouse.Lighthouse.Categories.GetAll()).Any())
            {
                return null;
                throw new InvalidOperationException("No or invalid Lighthouse test(s) provided.");
            }

            var projectStore = _projectStoreFactory.Create(State.CurrentProject.Id);
            sessionId ??= projectStore.CreateSession();
            string sessionDir = projectStore.GetSessionDirectory(sessionId);
            string lighthouseDir = Path.Combine(sessionDir, "lighthouse");
            Directory.CreateDirectory(lighthouseDir);
            // Target pages are shared between lighthouse and screenshooting.
            var selectedPages = State.CurrentProject.Options.ScreenshotOptions.TargetPages
                .Where(x => x.Value)
                .Select(x => x.Key);

            var lighthouse = new Lighthouse.Lighthouse(selectedTests);

            string CreateFilename(Uri url)
            {
                var filename = Utils.SanitizeFilename(url.AbsoluteUri);
                var filepath = Path.Combine(lighthouseDir, filename);
                return filepath;
            }

            await lighthouse.AnalyzeUrlsAsync(selectedPages, CreateFilename, token, progress);
            return sessionId;
        }

        public async Task RunSpider(CancellationToken? token = null, IProgress<TaskProgress>? progress = null)
        {
            var options = State.CurrentProject?.Options;
            if (options is null)
            {
                throw new InvalidOperationException("No project is loaded.");
            }
            _store.Dispatch(new Actions.ApplicationActions.SetIsCrawlingSite(true));
            var spiderLogger = _loggerFactory.CreateLogger<Spider.Spider>();
            Spider.Spider spider = new(options.SpiderOptions, options.Credentials, spiderLogger);
            var results = token is not null
                ? await spider.Crawl(token.Value, progress)
                : await spider.Crawl(progress);

            _store.Dispatch(new Actions.ProjectActions.SetCrawlResults(results));

            Dictionary<Uri, bool> newTargets = results.SitePages.ToDictionary(u => u, _ => true);
            MergePreviouslyDisabledPages(newTargets);
            SelectScreenshotUris(newTargets);
            _store.Dispatch(new Actions.ApplicationActions.SetIsCrawlingSite(false));

            void MergePreviouslyDisabledPages(Dictionary<Uri, bool> target)
            {
                var original = State.CurrentProject!.Options.ScreenshotOptions.TargetPages;
                target.Keys.Intersect(original.Keys)
                    .ForEach(k => target[k] = original[k]);
            }
        }

        public void ToggleTargetPageEnabled(Uri uri, bool enabled)
        {
            _store.Dispatch(new Actions.ProjectActions.ToggleTargetPageEnabled(uri, enabled));
        }

        public void SetTargetPages(IReadOnlyDictionary<Uri, bool> pages)
        {
            _store.Dispatch(new Actions.ProjectActions.SetTargetPages(pages));
        }

        public async Task<string> RunScreenshotter(CancellationToken? token, IProgress<TaskProgress>? progress = null, Device deviceFilter = ~Device.None, string? sessionId = null)
        {
            if (State.CurrentProject is null)
            {
                throw new InvalidOperationException("No project is opened.");
            }
            var projectStore = _projectStoreFactory.Create(State.CurrentProject.Id);
            _store.Dispatch(new Actions.ApplicationActions.SetIsTakingScreenshots(true));
            var logger = _loggerFactory.CreateLogger<ProjectScreenshotter>();
            sessionId ??= projectStore.CreateSession();
            var screenshotter = new ProjectScreenshotter(projectStore, logger, deviceFilter, sessionId);
            await screenshotter.TakeScreenshotsAsync(token, progress);
            var projectResults = projectStore?.GetResultsBySessionId() ?? new();

            _store.Dispatch(new Actions.ApplicationActions.SetIsTakingScreenshots(false));
            _store.Dispatch(new Actions.ProjectActions.SetProjectResults(projectResults.ToImmutableArray()));
            return sessionId;
        }

        public void SetScreenshotOptions(ScreenshotOptions options)
        {
            _store.Dispatch(new Actions.ProjectActions.SetScreenshotOptions(options));
        }

        public void SelectScreenshotUris(Dictionary<Uri, bool> uris)
        {
            _store.Dispatch(new Actions.ProjectActions.SetTargetPages(uris));
        }

        #region Scheduler

        public void AddOrEnableProject(Project project, TimeSpan? interval = null)
        {
            // This will read every project file and update the domain list.
            var domainLabel = string.Join(", ", project.SeedDomains());

            var existingScheduled = State.SchedulerState.ScheduledProjects
                .FirstOrDefault(p => p.ProjectId == project.Id);

            if (existingScheduled?.Enabled == true)
                return;

            interval ??= existingScheduled?.Interval ?? TimeSpan.FromMinutes(60);

            ScheduledProject schedProj = new(
                projectId: project.Id,
                projectName: project.Name,
                targetDomains: domainLabel,
                enabled: true,
                lastRun: existingScheduled?.LastRun,
                runImmediately: false,
                interval: interval.Value);

            IAction action = existingScheduled is object
                ? new Actions.SchedulerActions.UpdateScheduledProject(schedProj)
                : new Actions.SchedulerActions.AddScheduledProject(schedProj);
            _store.Dispatch(action);
        }

        public void Rename(string name)
        {
            _store.Dispatch(new Actions.ProjectActions.Rename(name));
        }

        public void DisableScheduledProject(string projectId, bool remove = false)
        {
            // This will read every project file and update the domain list.
            var existingScheduled = State.SchedulerState.ScheduledProjects
                .FirstOrDefault(p => p.ProjectId == projectId);

            if (existingScheduled is null || (!remove && !existingScheduled.Enabled))
                return;

            static ScheduledProject DisabledProj(ScheduledProject existing) =>
                existing with { Enabled = false, RunImmediately = false };

            IAction action = remove
                ? new Actions.SchedulerActions.RemoveProject(projectId)
                : new Actions.SchedulerActions.UpdateScheduledProject(DisabledProj(existingScheduled));
            _store.Dispatch(action);
        }

        public void SetCurrentlyScheduledProject(ScheduledProject schedProj)
        {
            IProjectStore store = _projectStoreFactory.Create(schedProj.ProjectId);
            _store.Dispatch(ActionCreators.RunScheduledProject(store));
        }

        private void UpdateScheduledProject(string id, Func<ScheduledProject, ScheduledProject> transform)
        {
            var schedProj = State.SchedulerState.ById(id);
            if (schedProj is null)
                throw new InvalidOperationException("This project must be added to the scheduler before being run.");
            var activatedProj = transform(schedProj);
            var action = new Actions.SchedulerActions.UpdateScheduledProject(activatedProj);
            _store.Dispatch(action);
        }

        public void RunScheduledProjectImmediately(string id)
        {
            UpdateScheduledProject(id, p => p with { Enabled = true, RunImmediately = true });
        }

        public void ChangeInterval(string id, TimeSpan interval)
        {
            UpdateScheduledProject(id, p => p with { Interval = interval });
        }

        public void ScheduledProjectIsComplete()
        {
            _store.Dispatch(new Actions.SchedulerActions.MarkScheduledProjectComplete());
        }

        public void ToggleScheduler(bool enabled)
        {
            _store.Dispatch(new Actions.SchedulerActions.ToggleScheduler(enabled));
        }

        #endregion Scheduler

        public void Dispose()
        {
        }

        public IDisposable Subscribe(IObserver<ApplicationState> observer) => _store.Subscribe(observer);
    }
}