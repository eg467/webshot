using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebShot.Menus
{
    //public class MenuOptionBuilder<TState, TInput, TProcessedInput>
    //{
    //    private readonly ConsoleMenuOption<TState, TInput, TProcessedInput> _option = new();

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> EntryDescription(string entryDescription, ConsoleColor? foregroundColor = null)
    //    {
    //        if (foregroundColor is not null)
    //        {
    //        }
    //        _option.de
    //        _option.EntryDescription = entryDescription;
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> Description(string description)
    //    {
    //        _option.Description = description;
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> MatchingPattern(string matcher)
    //    {
    //        _option.MatchingPattern = matcher;
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> SetAction(Action<Match> action)
    //    {
    //        _option.StatefulSelectionAction = (s, m, e) =>
    //        {
    //            action(m);
    //            return Task.CompletedTask;
    //        };
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> SetAction(Action<Match, OptionActionArgs> action)
    //    {
    //        _option.StatefulSelectionAction = (s, m, e) =>
    //        {
    //            action(m, e);
    //            return Task.CompletedTask;
    //        };
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> SetAction(Action<T?, Match> action)
    //    {
    //        _option.StatefulSelectionAction = (s, m, e) =>
    //        {
    //            action(s, m);
    //            return Task.CompletedTask;
    //        };
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> SetAction(Action<T?, Match, OptionActionArgs> action)
    //    {
    //        _option.StatefulSelectionAction = (s, m, e) =>
    //        {
    //            action(s, m, e);
    //            return Task.CompletedTask;
    //        };
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> SetAction(Func<Match, Task> action)
    //    {
    //        _option.StatefulSelectionAction = (s, m, e) => action(m);
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> SetAction(Func<T?, Match, Task> action)
    //    {
    //        _option.StatefulSelectionAction = (s, m, e) => action(s, m);
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> SetAction(Func<T?, Match, OptionActionArgs, Task> action)
    //    {
    //        _option.StatefulSelectionAction = action;
    //        return this;
    //    }

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> SetCompletionHandler(CompletionHandler completionHandler)
    //    {
    //        _option.CompletionNavigation = completionHandler;
    //        return this;
    //    }

    //    public MenuOption<T> Build()
    //    {
    //        // if the matching pattern is null, match on the entry description
    //        _option.MatchingPattern ??= _option.EntryDescription.ToString();

    //        return _option;
    //    }
    //}

    //public class MenuOptionBuilders
    //{
    //    public readonly static IMenuOption Root = new MenuOptionBuilders().Create()
    //        .Description("Root Menu")
    //        .EntryDescription("ROOT")
    //        .SetCompletionHandler(OptionCompletionNavigations.Root)
    //        .Build();

    //    public readonly static IMenuOption<TInput> BackMenu = new MenuOptionBuilders().Create()
    //        .Description("Go Back")
    //        .EntryDescription("BACK")
    //        .SetCompletionHandler(OptionCompletionNavigations.Back)
    //        .Build();

    //    public readonly static IMenuOption<TInput> Exit = new MenuOptionBuilders().Create()
    //        .Description("Exit Application")
    //        .EntryDescription("QUIT")
    //        .SetCompletionHandler(OptionCompletionNavigations.Exit)
    //        .Build();

    //    public MenuOptionBuilder<TState, TInput, TProcessedInput> CreateWithState<T>(T state) => new(state);

    //    public MenuOptionBuilder<bool> Create() => new();
    //}
}