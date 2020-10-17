using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NLog.Extensions.Logging;
using Redux;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WebShot;
using WebshotService;
using WebshotService.Entities;
using WebshotService.ProjectStore;
using WebshotService.State.Reducers;
using WebshotService.State.Store;

namespace Webshot
{
    public class Program
    {
        public static async Task Main()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var provider = BuildDi(config);

            using (provider as IDisposable)
            {
                // Force instantiation of independently running singletons.
                _ = provider.GetService<StoreWatcher>();
                _ = provider.GetService<NLogProjectTracker>();

                var host = provider.GetService<AppHost>();
                await host!.RunAsync();
            }
        }

        private static IServiceProvider BuildDi(IConfiguration config)
        {
            return new ServiceCollection()
                 .AddLogging(loggingBuilder =>
                 {
                     loggingBuilder
                         .ClearProviders()
                         .SetMinimumLevel(LogLevel.Information)
                         .AddNLog("NLog.config");
                 })
                .AddSingleton<FileProjectStoreFactory, FileProjectStoreFactory>()
                .AddSingleton<IProjectStoreFactory>(p => p.GetService<FileProjectStoreFactory>()!)
                .AddSingleton<IObjectStore<ApplicationState>>(_ => new FileStore<ApplicationState>("ApplicationState.json"))
                .AddSingleton<AppHost, AppHost>()
                .AddSingleton<NLogProjectTracker, NLogProjectTracker>()
                .AddSingleton<IStore<ApplicationState>>(p =>
                {
                    var store = p.GetService<IObjectStore<ApplicationState>>();
                    ApplicationState initial = store?.Exists == true ? store.Load() : new();
                    return new Store<ApplicationState>(Reducers.ApplicationReducer, initial);
                })
                .AddSingleton<ApplicationStore>()
                .AddSingleton<StoreWatcher>()
                .BuildServiceProvider();
        }
    }
}