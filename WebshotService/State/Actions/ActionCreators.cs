using System;
using System.Collections.Immutable;
using System.Linq;
using WebshotService.Entities;
using WebshotService.ProjectStore;

namespace WebshotService.State.Actions
{
    public static class ActionCreators
    {
        public static ActionCreator<ApplicationState> LoadProject(IProjectStore projectStore) =>
            (dispatch, getState) =>
            {
                if (projectStore is null)
                {
                    CloseProject();
                    return;
                }

                var project = projectStore.Load();

                dispatch(new ProjectActions.SetProject(project));

                var projectResults = projectStore?.GetResultsBySessionId() ?? new();
                dispatch(new ProjectActions.SetProjectResults(projectResults.ToImmutableArray()));

                dispatch(new RecentProjectsActions.Add(project.Id, project.Name));
            };

        public static ActionCreator<ApplicationState> CloseProject() =>
            (dispatch, getState) =>
            {
                dispatch(new ProjectActions.SetProject(null));
                dispatch(new ProjectActions.SetProjectResults(ImmutableArray<(string, SessionScreenshots)>.Empty));
            };

        public static ActionCreator<ApplicationState> RunScheduledProjectImmediately(string projectId) =>
            (dispatch, getState) =>
            {
                getState().SchedulerState.ScheduledProjects
                    .Where(p => p.ProjectId == projectId && !p.RunImmediately)
                    .Take(1)
                    .Select(p => p with { RunImmediately = true })
                    .ForEach(p => dispatch(new SchedulerActions.UpdateScheduledProject(p)));
            };
    }
}