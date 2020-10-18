using Redux;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using WebshotService.Entities;
using ApplicationActions = WebshotService.State.Actions.ApplicationActions;
using SchedulerActions = WebshotService.State.Actions.SchedulerActions;
using ProjectActions = WebshotService.State.Actions.ProjectActions;
using RecentProjectsActions = WebshotService.State.Actions.RecentProjectsActions;

namespace WebshotService.State.Reducers
{
    public static class Reducers
    {
        public static ApplicationState ApplicationReducer(ApplicationState state, IAction action)
        {
            var c = new ReducerCombiner<ApplicationState>(state, action);
            return c
                .Setter<ApplicationActions.SetApplication>(s => s.ApplicationState)
                .Reducer((s, a) =>
                    new ApplicationState()
                    {
                        CurrentProject = c.Sub(s.CurrentProject)
                            .Reducer(ProjectReducers.ProjectReducer)
                            .Reduce(),
                        ProjectResults = c.Sub(s.ProjectResults)
                            .Reducer(ProjectReducers.ResultsReducer)
                            .Reduce(),
                        IsTakingScreenshots = c.Sub(s.IsTakingScreenshots)
                            .Setter<ApplicationActions.SetIsTakingScreenshots>(x => x.IsTakingScreenshots)
                            .Reduce(),
                        IsCrawlingSite = c.Sub(s.IsCrawlingSite)
                            .Setter<ApplicationActions.SetIsCrawlingSite>(x => x.IsCrawling)
                            .Reduce(),
                        Progress = c.Sub(s.Progress)
                            .Setter<ApplicationActions.SetProgress>(x => x.Progress)
                            .Reduce(),
                        SchedulerState = c.Sub(s.SchedulerState)
                            .Reducer(SchedulerReducers.SchedulerReducer)
                            .Reduce(),
                        RecentProjects = c.Sub(s.RecentProjects)
                            .Reducer(ProjectReducers.ProjectHistory)
                            .Reduce()
                    })
                .Reduce();
        }

        internal static TResult SetterReducer<TAction, TResult>(TResult state, IAction action, Func<TAction, TResult> selector) =>
            (action is TAction a) ? selector(a) : state;
    }

    public static class ProjectReducers
    {
        public static Project? ProjectReducer(Project? state, IAction action)
        {
            var c = new ReducerCombiner<Project?>(state, action);
            return c
                .Setter<ProjectActions.SetProject>(s => s.Project)
                .Reducer((s, a) =>
                {
                    if (s is null) return null;
                    return new Project()
                    {
                        Id = s.Id,
                        Name = c.Sub(s.Name).Setter<ProjectActions.Rename>(x => x.Name).Reduce(),
                        Created = s.Created,
                        Options = c.Sub(s.Options).Reducer(OptionsReducer).Reduce(),
                        SpiderResults = c.Sub(s.SpiderResults).Reducer(CrawlResultReducer).Reduce()
                    };
                })
                .Reduce();
        }

        public static ImmutableArray<(string, SessionScreenshots)> ResultsReducer(
            ImmutableArray<(string, SessionScreenshots)> state,
            IAction action)
        {
            return action switch
            {
                ProjectActions.SetProjectResults setAction => setAction.Results.ToImmutableArray(),
                ProjectActions.AddProjectResult addAction => state.Add((addAction.Id, addAction.Results)),
                _ => state,
            };
        }

        public static Options OptionsReducer(Options state, IAction action)
        {
            var c = new ReducerCombiner<Options>(state, action);
            return c
                .Setter<ProjectActions.SetOptions>(x => x.Options)
                .Reducer((s, a) =>
                    new Options()
                    {
                        SpiderOptions = c.Sub(s.SpiderOptions)
                            .Setter<ProjectActions.SetSpiderOptions>(x => x.Options)
                            .Reduce(),
                        ScreenshotOptions = c.Sub(s.ScreenshotOptions)
                            .Setter<ProjectActions.SetScreenshotOptions>(x => x.Options)
                            .Reducer(ScreenshotOptionsReducer)
                            .Reduce(),
                        Credentials = c.Sub(s.Credentials)
                            .Setter<ProjectActions.SetProjectCredentials>(x => x.Credentials)
                            .Reduce()
                    })
                .Reduce();
        }

        public static ScreenshotOptions ScreenshotOptionsReducer(ScreenshotOptions state, IAction action)
        {
            return action switch
            {
                ProjectActions.SetTargetPages setTargetPagesAction => state with
                {
                    TargetPages = setTargetPagesAction.Pages.ToImmutableDictionary()
                },
                ProjectActions.ToggleTargetPageEnabled togglePageAction => state with
                {
                    TargetPages = state.TargetPages.SetItem(togglePageAction.Page, togglePageAction.Enabled)
                },
                _ => state,
            };
        }

        public static CrawlResults CrawlResultReducer(CrawlResults state, IAction action)
        {
            return action switch
            {
                ProjectActions.SetCrawlResults setAction => setAction.Results,
                _ => state,
            };
        }

        public static ImmutableArray<KeyValuePair<string, string>> ProjectHistory(
           ImmutableArray<KeyValuePair<string, string>> state,
           IAction action)
        {
            switch (action)
            {
                case RecentProjectsActions.Set setAction:
                    return setAction.History.ToImmutableArray();

                case RecentProjectsActions.Add addAction:
                    var newHistory = state;
                    newHistory = newHistory.RemoveAll(x => x.Key == addAction.Id);
                    var historyEntry = KeyValuePair.Create(addAction.Id, addAction.Description);
                    return newHistory.Add(historyEntry);

                case RecentProjectsActions.Clear clearAction:
                    return state.Clear();

                default:
                    return state;
            }
        }
    }

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