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

        public static bool Confirm(string prompt = "Confirm?")
        {
            static string Label(string[] values) => $"'{string.Join("' or '", values)}'";
            var yeses = new[] { "YES", "Y" };
            var nos = new[] { "NO", "N" };
            do
            {
                Console.WriteLine(prompt);
                Console.WriteLine($"Press {Label(yeses)} to confirm or {Label(nos)} to cancel.");
                var x = Console.ReadLine();
                if (x is null)
                    continue;

                if (yeses.Contains(x, StringComparer.OrdinalIgnoreCase))
                    return true;
                if (nos.Contains(x, StringComparer.OrdinalIgnoreCase))
                    return false;

                Console.WriteLine("Invalid input.");
            } while (true);
        }
    }

    public class ConsoleMenu : Menu<string>
    {
        public ConsoleMenu(
            List<IMenuOption<string>> options,
            MenuOutput output,
            Inputter<string> inputGetter)
            : base(options, output, inputGetter)
        {
        }
    }

    public class Menu<TInput> : IMenu, ICompletionHandler
    {
        protected readonly List<IMenuOption<TInput>> _options;
        private readonly MenuOutput _output;
        private readonly Inputter<TInput> _inputGetter;

        public CompletionHandler CompletionHandler { get; set; } =
            CompletionHandlers.Back;

        public Menu(
            IEnumerable<IMenuOption<TInput>> options,
            MenuOutput output,
            Inputter<TInput> inputGetter)
        {
            _options = new(options);
            _output = output;
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
                ColoredOutput headerOutput = new(_output.Header, menuColor);
                headerOutput.PrintHeader();
            }

            void DisplayDescription() => _output.Description.WriteLine();

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

    public class SilentMenu : IMenu, ICompletionHandler
    {
        private readonly Func<ICompletionHandler, Task> _action;
        public CompletionHandler CompletionHandler { get; set; }

        public SilentMenu(Func<ICompletionHandler, Task> action)
        {
            _action = action;
            CompletionHandler = CompletionHandlers.Back;
        }

        public Task<CompletionHandler> DisplayAsync() =>
            _action(this).ContinueWith(_ => CompletionHandler);
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