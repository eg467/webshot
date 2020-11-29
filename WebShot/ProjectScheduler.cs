using Microsoft.Extensions.Logging;
using Redux;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebShot.Menu;
using WebShot.Menu.ColoredConsole;
using WebshotService;
using WebshotService.Entities;
using WebshotService.State;
using WebshotService.State.Store;

namespace Webshot
{
    /// <summary>
    /// Runs projects automatically
    /// </summary>
    internal sealed class ProjectScheduler : IDisposable
    {
        private readonly ApplicationStore _appStore;
        private readonly ILogger<ProjectScheduler> _logger;
        private ApplicationState State => _appStore.State;

        private SchedulerState SchedulerState => State.SchedulerState;

        private readonly IObservable<long> _observable;

        private bool IsActive => State.SchedulerState.CurrentProject is not null;

        public ProjectScheduler(ApplicationStore appStore, ILogger<ProjectScheduler> logger)
        {
            _appStore = appStore;
            _logger = logger;
            var refreshInterval = TimeSpan.FromSeconds(10);
            _observable = Observable.Interval(refreshInterval);
        }

        public async Task Run()
        {
            using CancellableConsoleTask task = new();

            _appStore.ToggleScheduler(true);
            _observable.Throttle(TimeSpan.FromMinutes(1)).Subscribe(_ => DisplayUpcoming(), task.Token);
            await ReadRefreshStream(_observable.ToAsyncEnumerable(), () => CheckForNew(task), task.Token);
            _appStore.ToggleScheduler(false);
        }

        private async Task ReadRefreshStream(IAsyncEnumerable<long> source, Func<Task> action, CancellationToken token, int maxExceptions = 10)
        {
            var exceptionCount = 0;
            await foreach (var _ in source.WithCancellation(token))
            {
                try
                {
                    await action();
                }
                catch (OperationCanceledException)
                {
                    // Let the AsyncEnumerable handle this.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduler Error: {0}", ex);

                    if (++exceptionCount == maxExceptions)
                        throw;
                }
            }
        }

        private void DisplayUpcoming()
        {
            if (!SchedulerState.Enabled || IsActive)
                return;

            var outputter = ColoredOutput.ColoredFactory(ConsoleColor.Gray);
            SchedulerState.ScheduledProjects
                .Where(p => p.ScheduledFor is object)
                .Select(p => p.ToString())
                .Select(outputter)
                .ForEach(o => o.WriteLine());
        }

        private async Task CheckForNew(CancellableConsoleTask cancellableTask)
        {
            if (!SchedulerState.Enabled || IsActive)
                return;

            var next = State.NextScheduledProject();
            if (next is null)
                return;

            _appStore.SetCurrentlyScheduledProject(next);

            // Only check as in scheduled mode to save time.
            Task task = _appStore.RunScreenshotter(
                cancellableTask.Token,
                cancellableTask.Progress,
                Device.Desktop);

            await cancellableTask.CompleteOrCancel(task);
            _logger.LogInformation("Project ({0}) is complete.", next.ProjectName);

            _appStore.ScheduledProjectIsComplete();
        }

        public void Dispose()
        {
        }
    }
}