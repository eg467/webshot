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
using System.Drawing.Text;
using OpenQA.Selenium.DevTools.Debugger;
using WebshotService.Stats;

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
                    new OptionPrompt("Screenshots", "Take screenshots of web pages"),
                    completionHandler: CompletionHandlers.FromMenuCreator(ScreenshotsMenu)),

                new ConsoleOption(
                    new OptionPrompt("Stats", "View request statistics"),
                    asyncHandler: Handler,
                    completionHandler: CompletionHandlers.Repeat),

                new ConsoleOption(
                    new OptionPrompt(
                        "BrokenLinks",
                        $"See a list of broken links ({CurrentProject.SpiderResults.BrokenLinks.Count}) found in your site."),
                    asyncHandler: Handler,
                    completionHandler: CompletionHandlers.Repeat),

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

                    case "STATS":
                        Console.WriteLine("Page Request Statistics:");
                        var store = (FileProjectStore)_projectStoreFactory.Create(CurrentProject.Id);
                        var results = store.GetResultsByUri().OrderBy(x => x.Key);
                        foreach (var kv in results)
                        {
                            var uri = kv.Key;
                            var statsForUri = kv.Value
                                .Select(x => x.PageStats())
                                .OrderBy(x => x.Timestamp)
                                .ToList();

                            var timingsForUri = statsForUri
                                .Select(x => x.Timing)
                                .ToList();

                            RequestStatistics latest = statsForUri.Last();

                            Dictionary<string, NavigationTiming.TimingStats> stats = NavigationTiming.Stats(timingsForUri);

                            string[] fields = new[]
                            {
                                nameof(NavigationTiming.Ttfb),
                                nameof(NavigationTiming.Response),
                                nameof(NavigationTiming.FrontendProcessing),
                                nameof(NavigationTiming.Duration),
                                nameof(NavigationTiming.TransferSize)
                            };

                            List<(string opLabel, Func<NavigationTiming.TimingStats, double> operation)> operations = new()
                            {
                                ("Median", t => t.Median),
                                ("Min", t => t.Min),
                                ("Max", t => t.Max),
                            };

                            void WriteStats(string opLabel, Func<NavigationTiming.TimingStats, double> selector)
                            {
                                var timingArr = fields.Select(f => stats[f]).Select(selector).ToArray();
                                var formats = fields.Select((f, i) => $"{f}={timingArr[i]:N2}");
                                var formatLabel = string.Join(", ", formats);
                                Console.WriteLine($"{opLabel}: {formatLabel}");
                            }

                            ColoredOutput.WriteLines(uri.AbsoluteUri, "", $"Latest data ({latest.Timestamp}): ", latest.Timing.SerializeAll(), "");

                            Console.WriteLine(uri);
                            foreach (var (opLabel, operation) in operations)
                                WriteStats(opLabel, operation);
                        }

                        DefaultMenuLines.PressKeyToContinue();
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

        public IMenu ScreenshotsMenu()
        {
            List<IMenuOption<string>> options = new()
            {
                new ConsoleOption(
                    new OptionPrompt("Pages", "Choose what pages to download."),
                    completionHandler: CompletionHandlers.FromMenuCreator(SelectTargetPages)),

                new ConsoleOption(
                    new OptionPrompt("Options", "Choose screenshotting options."),
                    completionHandler: CompletionHandlers.FromMenuCreator(ScreenshotOptionsMenu)),

                new ConsoleOption(
                    new OptionPrompt("Run", "Take screenshots."),
                    asyncHandler: Handler,
                    completionHandler: CompletionHandlers.Back),
            };

            async Task Handler(Match _, ICompletionHandler _2)
            {
                ColoredOutput.WriteLines(Utils.FormattedSerialize(CurrentProject.Options.ScreenshotOptions));
                if (!DefaultMenuLines.Confirm("Would you like to take screenshots?"))
                    return;
                using CancellableConsoleTask task = new();
                Task screenshotTask = _appState.RunScreenshotter(task.Token, task.Progress);
                await task.CompleteOrCancel(screenshotTask);
            }

            ColoredOutput description = Utils.FormattedSerialize(CurrentProject.Options.ScreenshotOptions);
            return GenericNavigationMenu("Screenshots", description, options);
        }

        public IMenu ScreenshotOptionsMenu()
        {
            //public ImmutableDictionary<Uri, bool> TargetPages { get; init; } = ImmutableDictionary<Uri, bool>.Empty;
            //public ImmutableDictionary<Device, DeviceScreenshotOptions> DeviceOptions { get; init; }
            //public bool OverwriteResults { get; init; }
            //public bool HighlightBrokenLinks { get; init; }

            ScreenshotOptions Convert(ConverterFactory inputter)
            {
                ScreenshotOptions options = CurrentProject.Options.ScreenshotOptions;

                DeviceScreenshotOptions OptionsFor(Device key) =>
                    options.DeviceOptions.TryGetValue(key, out var value)
                        ? value!
                        : new Options().ScreenshotOptions.DeviceOptions[key];

                DeviceScreenshotOptions MakeDeviceOptionsFor(Device device)
                {
                    var existingOptions = OptionsFor(device);
                    var defaultEnabled = Maybe.From(existingOptions.Enabled);
                    bool deviceEnabled = inputter.New($"Take screenshots in {device} mode").Bool(defaultEnabled);
                    int deviceWidth = existingOptions.PixelWidth;
                    if (deviceEnabled)
                    {
                        var defaultWidth = Maybe.From(existingOptions.PixelWidth);
                        deviceWidth = inputter.New($"{device} width (px)").Int(defaultWidth, NumRange.AtLeast(300));
                    }
                    return new DeviceScreenshotOptions(device, deviceWidth, deviceEnabled);
                }

                var deviceOptionbuilder = ImmutableDictionary.CreateBuilder<Device, DeviceScreenshotOptions>();
                var devices = new[] { Device.Desktop, Device.Tablet, Device.Mobile };
                devices.ForEach(d => deviceOptionbuilder.Add(d, MakeDeviceOptionsFor(d)));

                return new ScreenshotOptions()
                {
                    DeviceOptions = deviceOptionbuilder.ToImmutable(),
                    HighlightBrokenLinks = inputter.New("Highlight broken links in screenshots").Bool(Maybe.From(options.HighlightBrokenLinks)),
                    OverwriteResults = inputter.New("Overwrite results").Bool(Maybe.From(options.OverwriteResults)),
                    TargetPages = options.TargetPages // Set these separately.
                };
            }

            Task Handler(ScreenshotOptions options, ICompletionHandler _)
            {
                _appState.SetScreenshotOptions(options);
                return Task.CompletedTask;
            }

            return new FactoryMenu<ScreenshotOptions>(
                header: "Screenshot settings",
                creator: Convert,
                asyncHandler: Handler,
                completionHandler: CompletionHandlers.Back);
        }

        public IMenu SelectTargetPages()
        {
            var items = CurrentProject.Options.ScreenshotOptions.TargetPages.Select(x => (x.Key, x.Value));
            return new ToggleMenu<Uri>(
                "Select pages to screenshoot",
                null,
                items: items,
                labeler: x => x.AbsoluteUri,
                handler: Handler,
                completionHandler: CompletionHandlers.Back);

            Task Handler(ListWithSelection<(Uri page, bool enabled)> pages, ICompletionHandler _)
            {
                Dictionary<Uri, bool> newPages = new();
                foreach (var page in pages.Items)
                    newPages[page.page] = page.enabled;
                _appState.SetTargetPages(newPages);
                return Task.CompletedTask;
            }
        }

        public IMenu SpiderMenu()
        {
            ColoredOutput description = Utils.FormattedSerialize(CurrentProject.Options.SpiderOptions);

            List<IMenuOption<string>> options = new List<IMenuOption<string>>{
                new ConsoleOption(
                    new OptionPrompt("Run", "Run the spider to find all web pages on the site"),
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