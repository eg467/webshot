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

namespace WebshotService.State.Store
{
    /// <summary>
    /// Facade for redux store and screenshotting tools
    /// </summary>
    public class ApplicationStateMachine : IDisposable, IObservable<ApplicationState>
    {
        public ApplicationState State => _store.GetState();
        private readonly IStore<ApplicationState> _store;
        private readonly IProjectStoreFactory _projectStoreFactory;
        private readonly IProgress<TaskProgress> _progress;

        public ApplicationStateMachine(IObjectStore<ApplicationState> initialStateStore, IProjectStoreFactory projectStoreFactory)
        {
            var initialState = initialStateStore.Exists ? initialStateStore.Load() : new();
            _store = new Store<ApplicationState>(Reducers.Reducers.ApplicationReducer, initialState);
            _projectStoreFactory = projectStoreFactory;
            _progress = new Progress<TaskProgress>(SetProgress);
        }

        public void SetState(ApplicationState state)
        {
            _store.Dispatch(new Actions.ApplicationActions.SetApplication(state));
        }

        public void SetSeedUris(IEnumerable<Uri> uris)
        {
            _store.Dispatch(new Actions.ProjectActions.SetSeedUris(uris));
        }

        public void SetSpiderOptions(SpiderOptions spiderOptions)
        {
            _store.Dispatch(new Actions.ProjectActions.SetSpiderOptions(spiderOptions));
        }

        public void SetSpiderWorking(bool status)
        {
            _store.Dispatch(new Actions.ApplicationActions.SetIsCrawlingSite(status));
        }

        public void SetCurrentProject(IProjectStore projectStore)
        {
            _store.Dispatch(ActionCreators.LoadProject(projectStore));
        }

        public void CloseCurrentProject()
        {
            _store.Dispatch(ActionCreators.CloseProject());
        }

        public void SetProgress(TaskProgress currentProgress)
        {
            _store.Dispatch(new Actions.ApplicationActions.SetProgress(currentProgress));
        }

        public async Task RunSpider(CancellationToken? token = null)
        {
            var options = State.CurrentProject?.Options;
            if (options is null)
            {
                throw new InvalidOperationException("No project is loaded.");
            }
            _store.Dispatch(new Actions.ApplicationActions.SetIsCrawlingSite(true));
            Spider.Spider spider = new(options.SpiderOptions, options.Credentials);
            var results = token is not null
                ? await spider.Crawl(token.Value, _progress)
                : await spider.Crawl(_progress);

            SetSpiderResults(results);
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

        public async Task RunScreenshotter(CancellationToken? token)
        {
            if (State.CurrentProject is null)
            {
                throw new InvalidOperationException("No project is opened.");
            }
            var projectStore = _projectStoreFactory.Create(State.CurrentProject.Id);
            _store.Dispatch(new Actions.ApplicationActions.SetIsTakingScreenshots(true));
            var screenshotter = new ProjectScreenshotter(projectStore);
            await screenshotter.TakeScreenshotsAsync(token, _progress);

            var projectResults = projectStore?.GetResultsBySessionId() ?? new();

            _store.Dispatch(new Actions.ApplicationActions.SetIsTakingScreenshots(false));
            _store.Dispatch(new Actions.ProjectActions.SetProjectResults(projectResults.ToImmutableArray()));
        }

        private void SetSpiderResults(CrawlResults results)
        {
            _store.Dispatch(new Actions.ProjectActions.SetCrawlResults(results));
        }

        public void SetScreenshotOptions(ScreenshotOptions options)
        {
            _store.Dispatch(new Actions.ProjectActions.SetScreenshotOptions(options));
        }

        public void SelectScreenshotUris(Dictionary<Uri, bool> uris)
        {
            _store.Dispatch(new Actions.ProjectActions.SetTargetPages(uris));
        }

        public void Dispose()
        {
        }

        public IDisposable Subscribe(IObserver<ApplicationState> observer) => _store.Subscribe(observer);
    }
}