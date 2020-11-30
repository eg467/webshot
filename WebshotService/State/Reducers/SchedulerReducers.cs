using Redux;
using System;
using System.Collections.Immutable;
using WebshotService.Entities;
using SchedulerActions = WebshotService.State.Actions.SchedulerActions;

namespace WebshotService.State.Reducers
{
    public static class SchedulerReducers
    {
        public static SchedulerState SchedulerReducer(SchedulerState state, IAction action)
        {
            switch (action)
            {
                case SchedulerActions.SetSchedulerOptions setAction:
                    return new SchedulerState(
                        setAction.Projects.ToImmutableArray(),
                        setAction.Enabled,
                        setAction.CurrentProject);

                case SchedulerActions.ToggleScheduler toggleAction:
                    return state with { Enabled = toggleAction.Enabled };

                case SchedulerActions.AddScheduledProject addAction:
                    {
                        return state with
                        {
                            ScheduledProjects = state.ScheduledProjects
                                .RemoveAll(x => x.ProjectId == addAction.NewScheduledProject.ProjectId)
                                .Add(addAction.NewScheduledProject)
                        };
                    }
                case SchedulerActions.MarkScheduledProjectComplete markCompletedAction:
                    {
                        if (state.CurrentProject is null)
                        {
                            throw new InvalidOperationException("No project is currently running.");
                        }

                        return state with
                        {
                            ScheduledProjects = state.ScheduledProjects.ImmutableConditionalMap(
                                p => Equals(p.ProjectId, state.CurrentProject?.Id),
                                p => p with { LastRun = DateTime.Now, RunImmediately = false }),
                            CurrentProject = null
                        };
                    }
                case SchedulerActions.UpdateScheduledProject updateAction:
                    {
                        return state with
                        {
                            ScheduledProjects =
                                state.ScheduledProjects.ImmutableConditionalMap(
                                    sp => sp.ProjectId.Equals(updateAction.Project.ProjectId, StringComparison.Ordinal),
                                    sp => updateAction.Project)
                        };
                    }
                case SchedulerActions.CurrentProjectChanged currentProjChangedAction:
                    {
                        return state with { CurrentProject = currentProjChangedAction.Project };
                    }
                case SchedulerActions.RemoveProject removeAction:
                    {
                        return state with
                        {
                            ScheduledProjects = state.ScheduledProjects.RemoveAll(p => p.ProjectId == removeAction.ProjectId)
                        };
                    }

                default:
                    return state;
            }
        }
    }
}