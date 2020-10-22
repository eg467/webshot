using System;
using System.Linq;
using System.Collections.Generic;
using WebShot.Menu.Options;
using WebShot.Menu.ColoredConsole;
using OpenQA.Selenium.DevTools.Runtime;

namespace WebShot.Menu.Menus
{
    public static class MenuBuilder
    {
        #region ConsoleMenu

        public record CreateConsoleMenuInput(IEnumerable<IMenuOption<string>> Options, MenuOutput Output);

        public static Menu<string> CreateConsoleMenu(CreateConsoleMenuInput input)
        {
            ColoredOutput prompt = "Enter Selection:";
            Inputter<string> prompter = Inputters.ConsolePrompt(prompt);
            return new(input.Options, input.Output, prompter);
        }

        #endregion ConsoleMenu

        #region SelectionMenu

        public static IMenu CreateSelectionMenu<T>(CreateSelectionMenuInput<T> input)
        {
            var options = new IMenuOption<ListWithSelection<T>>[]
            {
                new CustomOption<ListWithSelection<T>>(
                   null,
                   x => true,
                   input.SelectionHandler,
                   input.CompletionHandler)
            };

            var inputter = CreateInputter(completionHandler: options.First());

            return new Menu<ListWithSelection<T>>(options, input.Output, inputter);

            Inputter<ListWithSelection<T>> CreateInputter(ICompletionHandler completionHandler)
            {
                SelectionMenuInputter<T> inputter = new(input.Items, input.Output, input.Labeler)
                {
                    ColumnCount = input.ColumnCount,
                    CanCancel = input.CanCancel
                };

                if (input.KeyPressHandler is object)
                    inputter.KeyPressed += (s, e) => input.KeyPressHandler(e, completionHandler);

                return inputter.ChooseOption;
            }
        }

        public record CreateSelectionMenuInput<T>
        {
            public MenuOutput Output { get; init; }
            public IEnumerable<T> Items { get; init; }
            public Func<T, string> Labeler { get; init; }
            public AsyncOptionHandler<ListWithSelection<T>> SelectionHandler { get; init; }
            public CompletionHandler? CompletionHandler { get; init; }

            public int ColumnCount { get; init; } = 1;
            public bool CanCancel { get; init; } = true;
            public Action<KeyPressedEventArgs<T>, ICompletionHandler>? KeyPressHandler { get; init; }

            public CreateSelectionMenuInput(MenuOutput output, IEnumerable<T> items, AsyncOptionHandler<ListWithSelection<T>> selectionHandler, Func<T, string>? labeler = null)
            {
                Output = output ?? throw new ArgumentNullException(nameof(output));
                Items = items ?? throw new ArgumentNullException(nameof(items));
                Labeler = labeler ?? (x => x?.ToString() ?? "");
                SelectionHandler = selectionHandler ?? throw new ArgumentNullException(nameof(selectionHandler));
            }
        }

        #endregion SelectionMenu

        #region ToggleMenu

        public static IMenu CreateToggleMenu<T>(CreateToggleMenuInput<T> input)
        {
            var options = new IMenuOption<ListWithSelection<(T, bool)>>[]
            {
                new CustomOption<ListWithSelection<(T, bool)>>(
                   null,
                   x => true,
                   input.SelectionHandler,
                   input.CompletionHandler)
            };

            var inputter = CreateInputter(completionHandler: options.First());

            return new Menu<ListWithSelection<(T, bool)>>(options, input.Output, inputter);

            Inputter<ListWithSelection<(T, bool)>> CreateInputter(ICompletionHandler completionHandler)
            {
                var inputter = new ToggleMenuInputter<T>(input.Items, input.Output, input.Labeler)
                {
                    ColumnCount = input.ColumnCount,
                    CanCancel = input.CanCancel
                };

                if (input.KeyPressHandler is object)
                    inputter.KeyPressed += (s, e) => input.KeyPressHandler(e, completionHandler);

                return inputter.ChooseOption;
            }
        }

        public record CreateToggleMenuInput<T> : CreateSelectionMenuInput<(T, bool)>
        {
            public CreateToggleMenuInput(
                MenuOutput output,
                IEnumerable<(T, bool)> items,
                Func<T, string>? labeler,
                AsyncOptionHandler<ListWithSelection<(T, bool)>> selectionHandler)
            : base(
                  output,
                  items,
                  selectionHandler,
                  labeler: labeler is object
                    ? ((T item, bool enabled) x) => labeler!(x.item)
                    : (Func<(T, bool), string>?)null)
            {
            }
        }

        #endregion ToggleMenu
    }
}