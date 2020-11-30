using Redux;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using WebshotService.Entities;
using ProjectActions = WebshotService.State.Actions.ProjectActions;
using RecentProjectsActions = WebshotService.State.Actions.RecentProjectsActions;

namespace WebshotService.State.Reducers
{
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
            IAction action
        ) =>
            action switch
            {
                ProjectActions.SetProjectResults setAction => setAction.Results.ToImmutableArray(),
                ProjectActions.AddProjectResult addAction => state.Add((addAction.Id, addAction.Results)),
                _ => state,
            };

        public static Options OptionsReducer(Options state, IAction action)
        {
            ReducerCombiner<Options> c = new(state, action);
            return c.Setter<ProjectActions.SetOptions>(x => x.Options)
            .Reducer((s, a) =>
                new Options()
                {
                    SpiderOptions = c.Sub(s.SpiderOptions)
            .Setter<ProjectActions.SetSpiderOptions>(x => x.Options)
            .Reducer(SpiderOptionsReducer)
            .Reduce(),
                    ScreenshotOptions = c.Sub(s.ScreenshotOptions)
                        .Setter<ProjectActions.SetScreenshotOptions>(x => x.Options)
                        .Reducer(ScreenshotOptionsReducer)
                        .Reduce(),
                    Credentials = c.Sub(s.Credentials)
                        .Setter<ProjectActions.SetProjectCredentials>(x => x.Credentials)
                        .Reduce(),
                    LighthouseTests = c.Sub(s.LighthouseTests)
                        .Setter<ProjectActions.SetLighthouseTests>(x => x.Tests.ToArray())
                        .Reduce()
                })
            .Reduce();
        }

        public static SpiderOptions SpiderOptionsReducer(
            SpiderOptions state,
            IAction action
        ) =>
            action switch
            {
                ProjectActions.SetSeedUris setSeedAction => state with
                {
                    SeedUris = setSeedAction.Uris.ToImmutableArray()
                },
                _ => state,
            };

        public static ScreenshotOptions ScreenshotOptionsReducer(
            ScreenshotOptions state,
            IAction action
        ) =>
            action switch
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

        public static CrawlResults CrawlResultReducer(CrawlResults state, IAction action) =>
            action switch
            {
                ProjectActions.SetCrawlResults setAction => setAction.Results,
                _ => state,
            };

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
}