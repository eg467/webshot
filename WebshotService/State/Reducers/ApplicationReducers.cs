using Redux;
using System;
using WebshotService.Entities;
using ApplicationActions = WebshotService.State.Actions.ApplicationActions;
using System.Linq;

namespace WebshotService.State.Reducers
{
    public static class ApplicationReducers
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
}