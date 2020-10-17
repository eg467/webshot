using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WebShot.Menu;
using WebshotService.Entities;
using WebshotService.State.Store;
using WebShot.Menu.ColoredConsole;
using WebShot.Menu.Menus;
using WebShot.Menu.Options;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using WebshotService.ProjectStore;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;
using static WebShot.Menu.Menus.StringValidator;
using WebshotService;
using Microsoft.Extensions.Logging;
using WebshotService.Screenshotter;

namespace WebShot
{
    public class AppHost
    {
        private readonly ApplicationStore _appState;

        private ApplicationState State => _appState.State;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<AppHost> _logger;

        /// <summary>
        /// The currently opened project. This will be non-null outside of the root/non-project menus.
        /// </summary>
        private Project CurrentProject => State.CurrentProject!;

        private readonly MenuNavigator _menuNav;
        private readonly FileProjectStoreFactory _projectStoreFactory;

        private bool IsRoot => _menuNav.Count == 1;

        public AppHost(
            ApplicationStore appState,
            FileProjectStoreFactory projectStoreFactory,
            ILoggerFactory loggerFactory)
        {
            _appState = appState;
            _projectStoreFactory = projectStoreFactory;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<AppHost>();
            var menuNavLogger = _loggerFactory.CreateLogger<MenuNavigator>();
            _menuNav = new(menuNavLogger);
            _menuNav.Exited += (s, e) => Environment.Exit(0);
        }

        public async Task RunAsync()
        {
            await _menuNav.DisplayNew(MainMenu);
        }

        private void LoadProject(string path)
        {
            if (Directory.Exists(path))
                path = Path.Combine(path, FileProjectStore.ProjectFilename);

            _appState.SetCurrentProject(path);
        }

        private void CreateProject(string path)
        {
            _appState.SetCurrentProject(path);
        }

        private void CloseProject()
        {
            if (State.CurrentProject is object)
                _appState.CloseCurrentProject();
        }

        private void EnsureExists([NotNull] Project? project)
        {
            if (project is null)
                throw new InvalidOperationException("No project is currently loaded.");
        }

        #region Menus

        public IMenu GenericNavigationMenu(
            string header,
            IOutput? description,
            List<IMenuOption<string>> options)
        {
            var prompter = Inputters.ConsolePrompt((ColoredOutput)"Enter Selection:");
            description ??= ColoredOutput.Empty;
            var menu = new ConsoleMenu(options, header, description, prompter);
            menu.AddNavOptions(IsRoot);
            return menu;
        }

        public IMenu MainMenu()
        {
            CloseProject();
            var options = new List<IMenuOption<string>>{
                new ConsoleOption(
                    new OptionPrompt("Create <Project Directory Path>", "Create a project"),
                    handler: (m,_) => CreateProject(m.Groups["path"].Value),
                    matcher: new RegexOptionMatcher("create (?<path>.+)"),
                    completionHandler: CompletionHandlers.FromMenuCreator(ProjectMenu)),

                new ConsoleOption(
                    new OptionPrompt("Load <Project File or Directory Path>", "Load a project"),
                    handler: (m,_) => LoadProject(m.Groups["path"].Value),
                    matcher: new RegexOptionMatcher("load (?<path>.+)"),
                    completionHandler: CompletionHandlers.FromMenuCreator(ProjectMenu)),

                new ConsoleOption(
                    new OptionPrompt("Recent", "Select a recent project to open"),
                    completionHandler: CompletionHandlers.FromMenuCreator(ChooseRecentProject)),
            };

            return GenericNavigationMenu("Main Menu", null, options);
        }

        public IMenu ChooseRecentProject()
        {
            return new SelectionMenu<KeyValuePair<string, string>>(
                "Choose a Recent Project",
                null,
                State.RecentProjects.Reverse().Take(10),
                x => $"{x.Value} ({x.Key})",
                (r, c) =>
                {
                    LoadProject(r.Item.Key);
                    c.CompletionHandler = CompletionHandlers.FromMenuCreator(ProjectMenu);
                    return Task.CompletedTask;
                },
                CompletionHandlers.Back);
        }

        public IMenu ProjectMenu()
        {
            List<IMenuOption<string>> options = new List<IMenuOption<string>>{
                new ConsoleOption(
                    new OptionPrompt("'File' or 'Dir'", "Open project file or directory"),
                    asyncHandler: Handler,
                    matcher: new RegexOptionMatcher("file|dir")),
                new ConsoleOption(
                    new OptionPrompt("Spider", "Run the spider or set its options"),
                    completionHandler: CompletionHandlers.FromMenuCreator(SpiderMenu)),

                new ConsoleOption(
                    new OptionPrompt(
                        "BrokenLinks",
                        $"See a list of broken links ({CurrentProject.SpiderResults.BrokenLinks.Count}) found in your site."),
                    completionHandler: CompletionHandlers.FromMenuCreator(SpiderMenu)),

                new ConsoleOption(
                    new OptionPrompt("Reload", "Reload the current project from the file"),
                    asyncHandler: Handler)
            };

            Task Handler(Match m, ICompletionHandler c)
            {
                c.CompletionHandler = CompletionHandlers.Repeat;

                var projectPath = CurrentProject.Id;

                switch (m.Value.ToUpper())
                {
                    case "FILE":
                        Process.Start(projectPath);
                        break;

                    case "DIR":
                        var dir = Path.GetDirectoryName(projectPath) ?? throw new DirectoryNotFoundException();
                        Process.Start(dir);
                        break;

                    case "BROKENLINKS":
                        ColoredOutput.WriteLines(
                            "Broken Links Found...",
                            "(Run the spider to update this list.)");

                        var writer = ColoredOutput.ColoredFactory(ConsoleColor.Red);
                        CurrentProject.SpiderResults.BrokenLinks
                            .Select(l => $"--> {l.Target.AbsoluteUri}")
                            .OrderBy(x => x)
                            .Select(writer)
                            .ForEach(x => ColoredOutput.WriteLines(x));

                        ColoredOutput.WriteLines("Press any key to continue...");
                        Console.ReadKey();
                        break;

                    case "RELOAD":
                        LoadProject(projectPath);
                        break;
                }

                return Task.CompletedTask;
            }

            EnsureExists(State.CurrentProject);
            return GenericNavigationMenu(
                $"Project: {State.CurrentProject.Name} ({State.CurrentProject?.Id})",
                null,
                options);
        }

        public IMenu SpiderMenu()
        {
            ColoredOutput description = Utils.FormattedSerialize(CurrentProject.Options.SpiderOptions);

            List<IMenuOption<string>> options = new List<IMenuOption<string>>{
                new ConsoleOption(
                    new OptionPrompt("Run", "run the spider to find all web pages on the site"),
                    asyncHandler: RunSpiderHandler),
                new ConsoleOption(
                    new OptionPrompt("Options", "Set Options"),
                    completionHandler: CompletionHandlers.FromMenuCreator(SpiderOptionSeedMenu)),
            };

            async Task RunSpiderHandler(Match _, ICompletionHandler completionHandler)
            {
                using CancellableConsoleTask cancellableTask = new();
                var task = _appState.RunSpider(cancellableTask.Token, cancellableTask.Progress)
                    /*TODO: REMOVE*/ .ContinueWith(_ => Task.Delay(TimeSpan.FromDays(1)));
                await cancellableTask.CompleteOrCancel(task);
            }

            return GenericNavigationMenu("Spider", description, options);
        }

        public IMenu SpiderOptionSeedMenu()
        {
            EnsureExists(State.CurrentProject);
            var opts = State.CurrentProject.Options.SpiderOptions;

            return new FactoryMenu<SpiderOptions>(
                "Spider Options Menu",
                f => new SpiderOptions()
                {
                    SeedUris = f.New("Enter Seed URIs").MultilineSelect(lines => lines.Select(l => new Uri(l)).ToImmutableArray(), Maybe.From(opts.SeedUris)),
                    FollowInternalLinks = f.New("Follow links within the seed domains?").Bool(Maybe.From(opts.FollowInternalLinks)),
                    FollowExternalLinks = f.New("Follow links to external sites?").Bool(Maybe.From(opts.FollowExternalLinks)),
                    UriBlacklistPattern = f.New("Pattern to blacklist?", v => v.Custom(new RegexPatternValidator())).Str(Maybe.From(opts.UriBlacklistPattern))
                },
                (opt, _) => _appState.SetSpiderOptions(opt),
                completionHandler: CompletionHandlers.Back);
        }

        public static IMenu SimpsonsMenu()
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
                (ColoredOutput)"Choose a Simspsons Family Member",
                simpsonsMembers,
                x => x,
                Handler,
                CompletionHandlers.Back);
        }

        public static IMenu ManySimpsonsMenu()
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
                header: "Simpsons",
                description: null,
                items: simpsonsMembers,
                labeler: x => x,
                handler: Handler,
                completionHandler: CompletionHandlers.Back,
                3,
                true);
        }

        public static IMenu PeopleMenu()
        {
            static Person GetPerson(ConverterFactory f)
            {
                return new Person(
                    Name: f.New("Name", v => v.Length(NumRange.AtLeast(3))).Str(),
                    Age: f.New("Age").Int(range: NumRange.AtLeast(18)),
                    BirthDate: f.New("Birthdate (MM/DD/YYYY)", v => v.Regex(@"\d{2}/\d{2}/\d{4}")).Select(s => DateTime.Parse(s, Thread.CurrentThread.CurrentCulture)));
            }

            static Task Handler(Person person, ICompletionHandler completion)
            {
                string serializedPerson = JsonConvert.SerializeObject(person);
                List<ColoredOutput>? lines = ColoredOutput.ToList(null, null, "You serialized...", serializedPerson);
                MixedOutput.Vertical(lines).WriteLine();
                return Task.CompletedTask;
            }

            return new FactoryMenu<Person>("Make a person", GetPerson, Handler, CompletionHandlers.Back);
        }

        #endregion Menus

        public record Person(string Name, int Age, DateTime BirthDate);
    }
}