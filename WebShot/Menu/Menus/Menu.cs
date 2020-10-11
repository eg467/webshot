using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using WebshotService;
using WebShot.Menu.ColoredConsole;
using System.Runtime.CompilerServices;
using WebShot.Menu.Options;

namespace WebShot.Menu.Menus
{
    internal static class DefaultMenuLines
    {
        public static ColoredOutput InvalidResponse =
            ColoredOutput.Error("Invalid Response, please choose again.");

        public static void PressKeyToContinue()
        {
            new ColoredOutput("Press any key to continue...").WriteLine();
            _ = Console.ReadKey();
        }
    }

    public class ConsoleMenu : Menu<string>
    {
        public ConsoleMenu(
            List<IMenuOption<string>> options,
            IOutput description,
            Inputter<string> inputGetter) : base(options, description, inputGetter)
        {
        }
    }

    public class Menu<TInput> : IMenu
    {
        protected readonly List<IMenuOption<TInput>> _options;
        private readonly IOutput _description;
        private readonly Inputter<TInput> _inputGetter;

        public CompletionHandler CompletionHandler { get; set; } =
            OptionCompletionHandlers.Back;

        private readonly ConsoleColor _optionColor = ConsoleColor.Yellow;

        public Menu(
            List<IMenuOption<TInput>> options,
            IOutput description,
            Inputter<TInput> inputGetter)
        {
            _options = options;
            _description = description;
            _inputGetter = inputGetter;
        }

        public async Task<CompletionHandler> DisplayAsync()
        {
            string headerLine = "-----------------";
            while (true)
            {
                ColoredOutput.Multiline(ConsoleColor.Green, null, Enumerable.Repeat(headerLine, 3)).WriteLine();

                _description.WriteLine();

                var optionPrompts = _options.Select(o => o.Prompt()).Where(x => x is not null);
                if (optionPrompts.Any())
                {
                    ColoredOutput.Multiline(_optionColor, null, "", "Options", headerLine).WriteLine();
                    optionPrompts.ForEach(x => x?.WriteLine());
                }

                var input = _inputGetter();

                try
                {
                    var option = await ExecuteFirstMatchingOption(input);
                    if (option is not null) return option.CompletionHandler;

                    DefaultMenuLines.InvalidResponse.WriteLine();
                }
                catch (MenuInputException ex)
                {
                    ColoredOutput.Error(ex.Message).WriteLine();
                }
            }

            // Executes the handler for the first matching option and returns it.
            async Task<IMenuOption<TInput>?> ExecuteFirstMatchingOption(TInput l)
            {
                foreach (IMenuOption<TInput> option in _options)
                    if (await option.Execute(l))
                        return option;
                return null;
            }
        }

        public void AddOption(IMenuOption<TInput> option)
        {
            _options.Add(option);
        }
    }

    [Flags]
    public enum NavigationOptions
    {
        None = 0,
        Back = 1,
        Reload = 1 << 1,
        Exit = 1 << 2,
        Root = 1 << 3,
        All = Back | Reload | Exit | Root
    }

    public static class MenuExtensions
    {
        public static void AddNavOptions(this Menu<string> menu, bool isRoot, NavigationOptions options = NavigationOptions.All)
        {
            if (!isRoot)
            {
                if (options.HasFlag(NavigationOptions.Back))
                    menu.AddOption(new ConsoleMenuOption("Back", "Return to the previous menu", "back", completionHandler: OptionCompletionHandlers.Back));

                if (options.HasFlag(NavigationOptions.Root))
                    menu.AddOption(new ConsoleMenuOption("Root", "Return to the main menu", "root", completionHandler: OptionCompletionHandlers.Root));
            }

            if (options.HasFlag(NavigationOptions.Reload))
                menu.AddOption(new ConsoleMenuOption("Reset", "Reset Menu", "reset", completionHandler: OptionCompletionHandlers.Repeat));

            if (options.HasFlag(NavigationOptions.Exit))
                menu.AddOption(new ConsoleMenuOption("Exit/Quit", "Exit Application", "exit|quit", completionHandler: OptionCompletionHandlers.Exit));
        }
    }

    public interface IMenu
    {
        Task<CompletionHandler> DisplayAsync();
    }

    /// <summary>
    /// A function that returns a refreshed menu.
    /// </summary>
    /// <returns></returns>
    public delegate IMenu MenuCreator();
}