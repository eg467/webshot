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
                config.GetSection("ApplicationState").Value = JsonConvert.SerializeObject(new ApplicationState());
                var host = provider.GetService<AppHost>();
                await host!.RunAsync();
            }
        }

        private static IServiceProvider BuildDi(IConfiguration config)
        {
            return new ServiceCollection()
                .AddSingleton<IProjectStoreFactory, FileProjectStoreFactory>()
                .AddSingleton<IObjectStore<ApplicationState>>(p => new FileStore<ApplicationState>("ApplicationState.json"))
                .AddTransient<AppHost, AppHost>()
                .AddSingleton<IStore<ApplicationState>>(p =>
                {
                    var store = p.GetService<IObjectStore<ApplicationState>>();
                    ApplicationState initial = store?.Exists == true ? store.Load() : new();
                    return new Store<ApplicationState>(WebshotService.State.Reducers.Reducers.ApplicationReducer, initial);
                })
                .AddSingleton<ApplicationStateMachine>()
                .AddSingleton<StoreWatcher>()
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder
                        .ClearProviders()
                        .SetMinimumLevel(LogLevel.Trace)
                        .AddNLog(config);
                })
                .BuildServiceProvider();
        }
    }
}