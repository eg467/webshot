using System;
using System.Linq;
using System.Collections.Generic;
using WebShot.Menu.Options;
using WebShot.Menu.ColoredConsole;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebShot.Menu.Menus
{
    public record ConsoleMenuBuilderInput(IEnumerable<IMenuOption<string>> Options, MenuOutput Output);

    [Flags]
    public enum EnabledNavigations
    {
        None = 0,
        Back = 1 << 0,
        Reload = 1 << 1,
        Exit = 1 << 2,
        Root = 1 << 3,
        All = Back | Reload | Exit | Root
    }

    public class InputMenuBuilder
    {
        private readonly List<InputMenuOption> _options = new();
        private readonly MenuOutput _output;

        public InputMenuBuilder(MenuOutput output)
        {
            _output = output;
        }

        public Menu<string> BuildMenu()
        {
            ColoredOutput inputPrompt = "Enter Selection";
            Inputter<string> prompter = Inputters.ConsolePrompt(inputPrompt);

            // Add navigation options to the end of the option list.
            AddOptions(_navOptions);

            return new(_options, _output, prompter);
        }

        public IEmptyInputMenuOptionBuilder StartAddingOptions()
        {
            return new InputMenuOptionBuilder(this);
        }

        public InputMenuBuilder AddOptions(IEnumerable<InputMenuOptionOptions> optionData)
        {
            var newOptions = optionData.Select(d => new InputMenuOption(d));
            _options.AddRange(newOptions);
            return this;
        }

        private readonly List<InputMenuOptionOptions> _navOptions = new();

        public InputMenuBuilder IncludeNavOptions(bool isRoot, EnabledNavigations enabledNavigations = EnabledNavigations.All)
        {
            _navOptions.Clear();
            if (!isRoot)
            {
                if (enabledNavigations.HasFlag(EnabledNavigations.Back))
                {
                    OptionPrompt prompt = new("Back", "Return to the previous menu");
                    _navOptions.Add(new(prompt, CompletionHandlers.Back));
                }

                if (enabledNavigations.HasFlag(EnabledNavigations.Root))
                {
                    OptionPrompt prompt = new("Root", "Return to the main menu");
                    _navOptions.Add(new(prompt, CompletionHandlers.Root));
                }
            }

            if (enabledNavigations.HasFlag(EnabledNavigations.Exit))
            {
                OptionPrompt prompt = new("'Exit' or 'Quit'", "Exit the application");
                var opt = new InputMenuOptionOptions(prompt, _ => Task.CompletedTask)
                {
                    Matcher = new RegexOptionMatcher(/* language=regex */ @"exit|quit")
                };
                _navOptions.Add(opt);
            }

            return this;
        }

        /// <summary>
        /// Start out as an IEmptyInputMenuOptionBuilder to hide methods designed to modify the current option until the first option is added.
        /// </summary>
        public interface IEmptyInputMenuOptionBuilder
        {
            InputMenuOptionBuilder AddOption(OptionPrompt prompt, CompletionHandler completionHandler);

            InputMenuOptionBuilder AddOption(OptionPrompt prompt, MenuCreator menuCreator);

            InputMenuOptionBuilder AddOption(OptionPrompt prompt);

            InputMenuOptionBuilder DefaultOnSelect(AsyncOptionHandler<Match> handler);

            InputMenuOptionBuilder DefaultOnComplete(CompletionHandler handler);
        }

        public class InputMenuOptionBuilder : IEmptyInputMenuOptionBuilder
        {
            private readonly InputMenuBuilder _builder;

            private CompletionHandler _defaultCompletionHander = CompletionHandlers.Back;
            private AsyncOptionHandler<Match>? _defaultSelectionHandler;

            private readonly List<InputMenuOptionOptions> _options = new();
            private InputMenuOptionOptions CurrentOption => _options.Last();

            public InputMenuOptionBuilder(InputMenuBuilder builder)
            {
                _builder = builder;
            }

            public InputMenuOptionBuilder DefaultOnSelect(AsyncOptionHandler<Match> handler)
            {
                _defaultSelectionHandler = handler;
                return this;
            }

            public InputMenuOptionBuilder DefaultOnComplete(CompletionHandler completionHandler)
            {
                _defaultCompletionHander = completionHandler;
                return this;
            }

            public InputMenuOptionBuilder AddOption(OptionPrompt prompt, CompletionHandler completionHandler)
            {
                var options = new InputMenuOptionOptions(prompt, completionHandler);
                if (_defaultSelectionHandler is object)
                    options.SelectionHandler = _defaultSelectionHandler;
                _options.Add(options);
                return this;
            }

            public InputMenuOptionBuilder AddOption(OptionPrompt prompt, MenuCreator menuCreator)
            {
                return AddOption(prompt, CompletionHandlers.FromMenuCreator(menuCreator));
            }

            public InputMenuOptionBuilder AddOption(OptionPrompt prompt)
            {
                return AddOption(prompt, _defaultCompletionHander);
            }

            public InputMenuOptionBuilder OnSelect(AsyncOptionHandler<Match> handler)
            {
                CurrentOption.SelectionHandler = handler;
                return this;
            }

            public InputMenuOptionBuilder OnSelect(OptionHandler<Match> handler)
            {
                return OnSelect(handler.ToAsync());
            }

            public InputMenuOptionBuilder Validator(StringValidator validator)
            {
                CurrentOption.InputValidator = validator;
                return this;
            }

            public InputMenuOptionBuilder MatchOn(string pattern, bool matchCase = false, bool matchFullString = true)
            {
                CurrentOption.Matcher = new(pattern, matchCase, matchFullString);
                return this;
            }

            public InputMenuOptionBuilder OnComplete(CompletionHandler completionHandler)
            {
                CurrentOption.CompletionHandler = completionHandler;
                return this;
            }

            public InputMenuOptionBuilder OnComplete(MenuCreator menuCreator)
            {
                var completionHandler = CompletionHandlers.FromMenuCreator(menuCreator);
                return OnComplete(completionHandler);
            }

            public InputMenuBuilder BuildOptions()
            {
                _builder.AddOptions(_options);
                return _builder;
            }
        }
    }

    public static class MenuBuilder
    {
        #region ConsoleMenu

        public static InputMenuBuilder CreateInputMenu(MenuOutput output)
        {
            return new(output);
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