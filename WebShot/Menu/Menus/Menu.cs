using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using WebshotService;
using WebShot.Menu.ColoredConsole;
using System.Runtime.CompilerServices;
using WebShot.Menu.Options;
using Microsoft.Extensions.Logging;

namespace WebShot.Menu.Menus
{
    internal static class DefaultMenuLines
    {
        public static ColoredOutput InvalidResponse =
            ColoredOutput.Error("Invalid Response, please choose again.");

        public static void PressKeyToContinue()
        {
            Console.WriteLine("Press any key to continue...");
            _ = Console.ReadKey();
        }
    }

    public class ConsoleMenu : Menu<string>
    {
        public ConsoleMenu(
            List<IMenuOption<string>> options,
            string header,
            IOutput description,
            Inputter<string> inputGetter)
            : base(options, header, description, inputGetter)
        {
        }
    }

    public class Menu<TInput> : IMenu
    {
        protected readonly List<IMenuOption<TInput>> _options;
        private readonly string _header;
        private readonly IOutput _description;
        private readonly Inputter<TInput> _inputGetter;
        public CompletionHandler CompletionHandler { get; set; } =
            CompletionHandlers.Back;

        public Menu(
            List<IMenuOption<TInput>> options,
            string header,
            IOutput? description,
            Inputter<TInput> inputGetter)
        {
            _options = options;
            _header = header;
            _description = description ?? ColoredOutput.Empty;
            _inputGetter = inputGetter;
        }

        public async Task<CompletionHandler> DisplayAsync()
        {
            ConsoleColor menuColor = ConsoleColor.Yellow;
            while (true)
            {
                DisplayHeader();
                DisplayDescription();
                DisplayOptionPrompts();

                TInput input = _inputGetter();

                try
                {
                    IMenuOption<TInput>? option = await ExecuteFirstMatchingOption(input);
                    if (option is not null)
                        return option.CompletionHandler;

                    // No option was matched.
                    DefaultMenuLines.InvalidResponse.WriteLine();
                }
                catch (MenuInputException ex)
                {
                    ColoredOutput.Error(ex.Message).WriteLine();
                }
            }

            // HELPER FUNCTIONS

            void DisplayHeader()
            {
                ColoredOutput headerOutput = new(_header, menuColor);
                headerOutput.PrintHeader();
            }

            void DisplayDescription() => _description.WriteLine();

            void DisplayOptionPrompts()
            {
                ColoredOutput headerOutput = new("Options", menuColor);
                headerOutput.PrintHeader(hBorder: 3, borderChar: '-', hPadding: 2, vPadding: 0);
                _options.ForEach(o => o.Prompt?.WriteLine());
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
                    menu.AddOption(new ConsoleOption(
                        new OptionPrompt("Back", "Return to the previous menu"),
                        completionHandler: CompletionHandlers.Back));

                if (options.HasFlag(NavigationOptions.Root))
                    menu.AddOption(new ConsoleOption(
                        new OptionPrompt("Root", "Return to the main menu"),
                        completionHandler: CompletionHandlers.Root));
            }

            if (options.HasFlag(NavigationOptions.Exit))
                menu.AddOption(new ConsoleOption(
                    new OptionPrompt("'Exit' or 'Quit'", "Exit the application"),
                    matcher: new RegexOptionMatcher(/* language=regex */ @"exit|quit"),
                    completionHandler: _ => Task.CompletedTask /* CompletionHandlers.Exit*/));
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