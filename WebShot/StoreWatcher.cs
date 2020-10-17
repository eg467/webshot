using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Redux;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using WebshotService;
using WebshotService.Entities;
using WebshotService.ProjectStore;

namespace Webshot
{
    /// <summary>
    /// Persists changes of application state to the file store.
    /// </summary>
    internal sealed class StoreWatcher : IDisposable
    {
        public event EventHandler? ProjectChanged;

        public ApplicationState State => _reduxStore.GetState();

        private readonly IStore<ApplicationState> _reduxStore;
        private readonly IObjectStore<ApplicationState> _fileStore;
        private readonly FileProjectStoreFactory _projectStoreFactory;
        private readonly List<IDisposable> _listeners = new();
        private readonly ILogger<StoreWatcher> _logger;

        public StoreWatcher(
            IStore<ApplicationState> reduxStore,
            IObjectStore<ApplicationState> fileStore,
            ILogger<StoreWatcher> logger,
            FileProjectStoreFactory projectStoreFactory)
        {
            _reduxStore = reduxStore;
            _fileStore = fileStore;
            _logger = logger;
            _listeners.Add(_reduxStore.DistinctUntilChanged().Subscribe(AppHandler));
            _listeners.Add(_reduxStore.Select(s => s.CurrentProject).DistinctUntilChanged().Subscribe(ProjectHandler));
            _projectStoreFactory = projectStoreFactory;
        }

        private void ProjectHandler(Project? project)
        {
            ProjectChanged?.Invoke(this, EventArgs.Empty);

            if (project is null)
                return;

            IProjectStore store = _projectStoreFactory.Create(project.Id);
            store.Save(project);
        }

        private void AppHandler(ApplicationState state)
        {
            // Only save a subset of state to disk, force a refresh on reopening.
            ApplicationState stateToSave = new()
            {
                SchedulerState = state.SchedulerState with { Enabled = false, CurrentProject = null },
                RecentProjects = state.RecentProjects,
            };

            var logmsg = $"Saving State: {JsonConvert.SerializeObject(stateToSave)}";
            _logger.LogTrace(logmsg);
            _fileStore.Save(stateToSave);
        }

        public void Dispose()
        {
            _listeners.ForEach(l => l.Dispose());
        }
    }
}