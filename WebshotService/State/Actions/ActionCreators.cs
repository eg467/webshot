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
                if (!projectStore.Exists)
                {
                    throw new InvalidOperationException($"The project ({projectStore.Id}) does not exist");
                }

                var project = projectStore.Load();

                dispatch(new ProjectActions.SetProject(project));

                var projectResults = projectStore?.GetResultsBySessionId() ?? new();
                dispatch(new ProjectActions.SetProjectResults(projectResults.ToImmutableArray()));

                dispatch(new RecentProjectsActions.Add(project.Id, $"{project.Name} ({project.Id})"));
            };

        public static ActionCreator<ApplicationState> CloseProject() =>
            (dispatch, getState) =>
            {
                dispatch(new ProjectActions.SetProject(null));
                dispatch(new ProjectActions.SetProjectResults(ImmutableArray<(string, ScreenshotResults)>.Empty));
            };

        //public static ActionCreator<ApplicationState> UpdateScheduledProjectsDetails(IProjectStoreFactory projectStoreFactory) =>
        //    (dispatch, getState) =>
        //    {
        //        ApplicationState state = getState();
        //        SchedulerState schedulerState = state.SchedulerState;
        //        ImmutableArray<ScheduledProject> scheduledProjects = schedulerState.ScheduledProjects;

        //        Project currentProject = schedulerState.CurrentProject;

        //        var newScheduledProjectList = scheduledProjects
        //            .Select(p =>
        //            {
        //                var project = projectStoreFactory.Create(p.ProjectId).Load();
        //                var domains = project.Options.ScreenshotOptions.TargetPages.Select(u => u.AbsoluteUri);
        //                var domainLabel = string.Join(", ", domains);

        //                if (p.ProjectId == schedulerState.CurrentProject?.Id)
        //                {
        //                    currentProject = project;
        //                }

        //                return new ScheduledProject(
        //                    p.ProjectId,
        //                    project.Name,
        //                    domainLabel,
        //                    p.Enabled,
        //                    p.LastRun,
        //                    p.RunImmediately,
        //                    p.Interval);
        //            })
        //            .ToList();

        //        dispatch(new SchedulerActions.SetSchedulerOptions()
        //        {
        //            CurrentProject = currentProject,
        //            Enabled = schedulerState.Enabled,
        //            Projects = newScheduledProjectList
        //        });
        //    };

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