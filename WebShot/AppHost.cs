using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using WebShot.Menu;
using WebshotService.Entities;
using WebshotService.State.Store;
using WebShot.Menu.ColoredConsole;
using WebShot.Menu.Menus;
using WebShot.Menu.Options;

namespace WebShot
{
    public class AppHost
    {
        private readonly ApplicationStateMachine _appState;
        private readonly AppMenus _menus;
        private readonly MenuNavigator _menuNav = new();

        public AppHost(ApplicationStateMachine appState)
        {
            _appState = appState;
            _menus = new AppMenus(_appState, () => _menuNav.Count == 1);

            _menuNav.Exited += (s, e) => Environment.Exit(0);
        }

        public Task RunAsync()
        {
            return _menuNav.DisplayNew(_menus.MainMenu);
        }
    }

    public class AppMenus
    {
        private readonly ApplicationStateMachine _appState;
        private readonly Func<bool> _isRoot;

        private ApplicationState State => _appState.State;

        public AppMenus(ApplicationStateMachine appState, Func<bool> isRoot)
        {
            _appState = appState;
            _isRoot = isRoot;
        }

        public Menu<string> MainMenu()
        {
            var options = new List<IMenuOption<string>>{
                new ConsoleMenuOption("Ping", "Enter 'ping' to see 'pong'.", "ping", (m,_)=> { Console.WriteLine("Pong"); return Task.CompletedTask; }),
                new ConsoleMenuOption("Simpsons", "Choose a Simpson.", "simpsons", null, OptionCompletionHandlers.FromMenuCreator(SimpsonsMenu)),
                new ConsoleMenuOption("ManySimpsons", "Choose Several Simpsons.", "ms|manysimpsons", null, OptionCompletionHandlers.FromMenuCreator(ManySimpsonsMenu)),
            };

            var menu = new ConsoleMenu(
                options,
                new ColoredOutput("Main Menu"),
                Inputters.ConsolePrompt("Enter Selection:"));

            menu.AddNavOptions(_isRoot());

            return menu;
        }

        public Menu<ListWithSelection<string>> SimpsonsMenu()
        {
            static Task Handler(ListWithSelection<string> r, ICompletionHandler _)
            {
                new ColoredOutput(r.Item, ConsoleColor.DarkMagenta)
                    .FormatLines("You selected: {0}")
                    .WriteLine();
                return Task.CompletedTask;
            }

            var simpsonsMembers = new[] { "Homer", "Marge", "Bart", "Lisa", "Maggie" };

            return new SelectionMenu<string>(
                "Simpsons",
                simpsonsMembers,
                x => x,
                Handler,
                OptionCompletionHandlers.Back);
        }

        public Menu<ListWithSelection<(string, bool)>> ManySimpsonsMenu()
        {
            static Task Handler(ListWithSelection<(string Item, bool Enabled)> r, ICompletionHandler _)
            {
                if (r.SelectedIndex == -1)
                {
                    new ColoredOutput("User cancelled.").WriteLine();
                    return Task.CompletedTask;
                }

                var selectedPeople = r.Items
                    .Where(x => x.Enabled)
                    .Select(x => new ColoredOutput(x.Item, ConsoleColor.DarkMagenta, ConsoleColor.White));

                MixedOutput.Vertical(selectedPeople).WriteLine();
                return Task.CompletedTask;
            }

            var simpsonsMembers = new[] { "Homer", "Marge", "Bart", "Lisa", "Maggie" };

            return new ToggleMenu<string>(
                "Simpsons",
                simpsonsMembers,
                x => x,
                Handler,
                OptionCompletionHandlers.Back,
                3,
                true);
        }
    }
}