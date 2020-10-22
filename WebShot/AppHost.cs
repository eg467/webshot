using System;
using WebshotService.State;
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
using WebshotService.ProjectStore;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;
using static WebShot.Menu.Menus.StringValidator;
using WebshotService;
using Microsoft.Extensions.Logging;
using WebshotService.Stats;
using Webshot;

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

        private FileProjectStore CurrentProjectStore =>
            (FileProjectStore)_projectStoreFactory.Create(CurrentProject.Id);

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

        private void CloseProject()
        {
            if (State.CurrentProject is object)
                _appState.CloseCurrentProject();
        }

        private void DeleteProject(string id)
        {
            _appState.DeleteProject(id);
            var store = (FileProjectStore)_projectStoreFactory.Create(id);
            store.Delete();
        }

        private void EnsureExists([NotNull] Project? project)
        {
            if (project is null)
                throw new InvalidOperationException("No project is currently loaded.");
        }

        #region Menus

        public IMenu GenericNavigationMenu(MenuOutput output, List<IMenuOption<string>> options)
        {
            Menu<string> menu = MenuBuilder.CreateConsoleMenu(new(options, output));
            menu.AddNavOptions(IsRoot);
            return menu;
        }

        public IMenu MainMenu()
        {
            CloseProject();
            var options = new List<IMenuOption<string>>{
                new ConsoleOption(
                    new OptionPrompt("Create", "Create a project"),
                    asyncHandler: CreateProject,
                    completionHandler: CompletionHandlers.FromMenuCreator(ProjectMenu)),

                new ConsoleOption(
                    new OptionPrompt("Load <Project File or Directory Path>", "Load a project"),
                    handler: (m,_) => LoadProject(m.Groups["path"].Value),
                    matcher: new RegexOptionMatcher("load (?<path>.+)"),
                    completionHandler: CompletionHandlers.FromMenuCreator(ProjectMenu)),

                new ConsoleOption(
                    new OptionPrompt("Recent", "Select a recent project to open"),
                    completionHandler: CompletionHandlers.FromMenuCreator(ChooseRecentProjectMenu)),

                new ConsoleOption(
                    new OptionPrompt("Scheduler", "Scheduler options and operation"),
                    completionHandler: CompletionHandlers.FromMenuCreator(SchedulerMenu)),
            };

            Task CreateProject(Match m, ICompletionHandler handler)
            {
                InputParserFactory inputter = new();

                string projectPathOrDir = inputter.New(
                    "Directory or filename of new project",
                    v => v.Predicate(
                        p => File.Exists(p) || Directory.Exists(p),
                        "Invalid file or directory path"))
                .Str();
                _appState.SetCurrentProject(projectPathOrDir);

                var uri = inputter.New("Enter a seed URI (you can add more later)").Uri();
                _appState.SetSeedUris(new[] { uri });

                var name = inputter.New("Enter project name").Str(Maybe.From(uri.Authority));
                _appState.Rename(name);
                return Task.CompletedTask;
            }

            return GenericNavigationMenu(new("Main Menu"), options);
        }

        public IMenu SchedulerMenu()
        {
            var schedState = State.SchedulerState;
            ScheduledProject ChangeInterval(string projectId, int newIntervalMins)
            {
                var scheduledProject = schedState.ById(projectId)
                    ?? throw new ArgumentException("Invalid project ID.", nameof(projectId));
                return scheduledProject with { Interval = TimeSpan.FromMinutes(newIntervalMins) };
            }

            var options = new List<IMenuOption<string>>{
                new ConsoleOption(
                    new OptionPrompt("Run", "Schedule projects for automatic execution"),
                    asyncHandler: (_,_2) => RunScheduler(),
                    completionHandler: CompletionHandlers.Repeat),

                new ConsoleOption(
                    new OptionPrompt("Choose", "Choose which projects should run automatically"),
                    completionHandler: CompletionHandlers.FromMenuCreator(EnableScheduledProjectsMenu)),

                new ConsoleOption(
                    new OptionPrompt("Interval <Interval in Minutes> <Project File>", "Create a project", true),
                    handler: (m,_) => ChangeInterval(m.Groups["projectId"].Value, int.Parse(m.Groups["interval"].Value)),
                    matcher: new RegexOptionMatcher( /* language=regex */ @"interval\s+(?<interval>\d+)\s+(?<projectId>.*)"),
                    completionHandler: CompletionHandlers.Repeat),

                new ConsoleOption(
                    new OptionPrompt("Now", "Schedule a project for an immediate run"),
                    completionHandler: CompletionHandlers.FromMenuCreator(ChooseImmediateRunMenu)),
            };

            IMenu ChooseImmediateRunMenu() =>
                MenuBuilder.CreateSelectionMenu<ScheduledProject>(new(
                    output: new("Choose a project to run immediately"),
                    items: schedState!.ScheduledProjects,
                    labeler: p => p.ToString(),
                    selectionHandler: (x, c) =>
                    {
                        _appState.RunScheduledProjectImmediately(x.Item.ProjectId);
                        c.CompletionHandler = CompletionHandlers.Back;
                        return Task.CompletedTask;
                    }));

            async Task RunScheduler()
            {
                var logger = _loggerFactory.CreateLogger<ProjectScheduler>();
                ProjectScheduler scheduler = new(this._appState, logger);
                await scheduler.Run();
            }

            Func<string, ColoredOutput> outputter = ColoredOutput.ColoredFactory(ConsoleColor.Gray);
            IEnumerable<ColoredOutput> descriptionLines = State.SchedulerState.ScheduledProjects
                .Where(p => p.ScheduledFor is object)
                .Select(p => outputter(p.ToString()));

            MixedOutput description = MixedOutput.Vertical(descriptionLines);
            return GenericNavigationMenu(new("Scheduler Menu", description), options);
        }

        public IMenu EnableScheduledProjectsMenu()
        {
            // Recently opened projects.

            //var recent = State.RecentProjects
            //    .Select(x => KeyValuePair.Create(x.Key, (x.Value, false)));
            var scheduledProjects = State.SchedulerState.ScheduledProjects;

            Dictionary<string, string> itemDict = new(State.RecentProjects);
            // Projects that are currently scheduled but might not have been recently opened.
            foreach (var p in scheduledProjects)
                itemDict[p.ProjectId] = p.ProjectName;

            bool IsScheduled(string id) =>
                State.SchedulerState.ById(id)?.Enabled == true;

            var items = itemDict
                .Select(x => (IdName: x, Enabled: IsScheduled(x.Key)))
                .OrderBy(x => x.IdName.Value);

            Task Handler(ListWithSelection<(KeyValuePair<string, string> project, bool enabled)> projects, ICompletionHandler completion)
            {
                foreach (var selection in projects.Items)
                {
                    if (selection.enabled)
                    {
                        var store = _projectStoreFactory.Create(selection.project.Key);
                        var project = store.Load();
                        _appState.AddOrEnableProject(project);
                    }
                    else
                    {
                        _appState.DisableScheduledProject(selection.project.Key);
                    }
                }
                completion.CompletionHandler = CompletionHandlers.Back;
                return Task.CompletedTask;
            }

            return MenuBuilder.CreateToggleMenu<KeyValuePair<string, string>>(new(
                output: new("De/Schedule recent projects to automatically run"),
                items: items,
                labeler: (x => $"{x.Value} ({x.Key})"),
                selectionHandler: Handler));
        }

        public IMenu ChooseRecentProjectMenu()
        {
            async Task ChooseProject(ICompletionHandler completion)
            {
                var projectId = await ChooseRecentProject();
                if (projectId is null)
                {
                    completion.CompletionHandler = CompletionHandlers.Back;
                    return;
                }
                LoadProject(projectId);
                completion.CompletionHandler = CompletionHandlers.FromMenuCreator(ProjectMenu);
            }

            return new SilentMenu(ChooseProject);
        }

        private Task<string?> ChooseRecentProject(int maxProjects = 10)
        {
            string? result = null;

            var input = new MenuBuilder.CreateSelectionMenuInput<KeyValuePair<string, string>>(
                new("Choose a recent Project"),
                this.State.RecentProjects.Reverse().Take(maxProjects),
                (r, c) =>
                {
                    result = r.Item.Key;
                    return Task.CompletedTask;
                },
                x => $"{x.Value} ({x.Key})")
            {
                KeyPressHandler = (args, completion) =>
                {
                    if (args.Key == ConsoleKey.Delete)
                    {
                        string projectId = args.Item.Key;
                        string projectName = args.Item.Value;
                        string prompt = $"Do you want to delete project: {projectName} [{projectId}] and all its files?";
                        if (DefaultMenuLines.Confirm(prompt))
                            DeleteProject(projectId);

                        completion.CompletionHandler = CompletionHandlers.Repeat;
                    }
                }
            };

            var menu = MenuBuilder.CreateSelectionMenu(input);
            return menu.DisplayAsync().ContinueWith(_ => result);
        }

        public IMenu ProjectMenu()
        {
            List<IMenuOption<string>> options = new List<IMenuOption<string>>{
                new ConsoleOption(
                    new OptionPrompt("'File' or 'Dir'", "Open project file or directory"),
                    asyncHandler: Handler,
                    matcher: new RegexOptionMatcher("file|dir")),

                new ConsoleOption(
                    new OptionPrompt("Rename <Name?>", "Rename this project (Leave blank to use the domain)"),
                    asyncHandler: Handler,
                    matcher: new RegexOptionMatcher(/* language=regex */ @"(?<op>rename)\s*(?<name>.*)")),

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
                    new OptionPrompt("Creds", "Set basic authentication credentials"),
                    completionHandler: CompletionHandlers.FromMenuCreator(SetCredentialsMenu)),

                new ConsoleOption(
                    new OptionPrompt("ClearCreds", "Clear all basic authentication credentials"),
                    handler: (x,_) => { _appState.SetCredentials(new()); },
                    completionHandler: CompletionHandlers.Back),

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

            IMenu SetCredentialsMenu()
            {
                return new FactoryMenu<ProjectCredentials>("Enter Basic Authentication Credentials", p =>
                {
                    string seedDomain = this.CurrentProject.SeedDomains().First();
                    Dictionary<Uri, AuthCredentials> creds = new();

                    do
                    {
                        Maybe<Uri> defaultDomain = Maybe.From(new Uri(seedDomain));
                        var credDomain = p.New("Domain").Uri(defaultDomain);
                        string user = p.New("User").Str();
                        string password = p.New("Password").Str();
                        creds[credDomain] = new(user, password, encrypted: true);
                    } while (DefaultMenuLines.Confirm("Enter another credential?"));

                    return new ProjectCredentials()
                    {
                        CredentialsByDomain = creds.ToImmutableDictionary()
                    };
                },
                (x, c) => this._appState.SetCredentials(x),
                completionHandler: CompletionHandlers.Back);
            }

            Task Handler(Match m, ICompletionHandler c)
            {
                c.CompletionHandler = CompletionHandlers.Repeat;

                var projectPath = CurrentProject.Id;

                // Match on the entire word, or use 'op' group to specify command

                switch (m.Groups["op"]?.Value?.ToUpper() ?? m.Value.ToUpper())
                {
                    case "FILE":
                        Process.Start(projectPath);
                        break;

                    case "DIR":
                        var dir = Path.GetDirectoryName(projectPath) ?? throw new DirectoryNotFoundException();
                        Process.Start(dir);
                        break;

                    case "RENAME":
                        var name = m.Groups["name"].Value.EmptyToNull()
                            ?? CurrentProject.SeedDomains().FirstOrDefault()
                            ?? "Webshot Project";

                        _appState.Rename(name);
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
                        var results = CurrentProjectStore.GetResultsByUri().OrderBy(x => x.Key);
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

            return GenericNavigationMenu(
                output: new($"Project: {CurrentProject.Name} ({CurrentProject?.Id})"),
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
            return GenericNavigationMenu(new("Screenshots", description), options);
        }

        public IMenu ScreenshotOptionsMenu()
        {
            //public ImmutableDictionary<Uri, bool> TargetPages { get; init; } = ImmutableDictionary<Uri, bool>.Empty;
            //public ImmutableDictionary<Device, DeviceScreenshotOptions> DeviceOptions { get; init; }
            //public bool OverwriteResults { get; init; }
            //public bool HighlightBrokenLinks { get; init; }

            ScreenshotOptions Convert(InputParserFactory inputter)
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
            var input = new MenuBuilder.CreateToggleMenuInput<Uri>(
                output: new("Selected pages to screenshoot"),
                items: CurrentProject.Options.ScreenshotOptions.TargetPages.Select(x => (x.Key, x.Value)),
                labeler: x => x.AbsoluteUri,
                selectionHandler: Handler);

            return MenuBuilder.CreateToggleMenu(input);

            Task Handler(ListWithSelection<(Uri page, bool enabled)> pages, ICompletionHandler completion)
            {
                Dictionary<Uri, bool> newPages = new();
                foreach (var page in pages.Items)
                    newPages[page.page] = page.enabled;
                _appState.SetTargetPages(newPages);
                completion.CompletionHandler = CompletionHandlers.Back;
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

            return GenericNavigationMenu(output: new("Spider", description), options);
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
                    UriBlacklistPattern = f.New("Pattern to blacklist?", v => v.IsRegexPattern()).Str(Maybe.From(opts.UriBlacklistPattern))
                },
                (opt, _) => _appState.SetSpiderOptions(opt),
                completionHandler: CompletionHandlers.Back);
        }

        #endregion Menus
    }
}