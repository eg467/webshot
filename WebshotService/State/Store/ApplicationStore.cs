using Redux;
using System.Reactive.Linq;
using System;
using System.IO;
using System.Reflection;
using WebshotService.Entities;
using WebshotService.ProjectStore;
using System.Threading.Tasks;

namespace WebshotService.State.Store
{
    public static class ApplicationStore
    {
        public static readonly Store<ApplicationState> Store;

        public static ApplicationState State => Store.GetState();

        public static Project? Project => State.CurrentProject;

        static ApplicationStore()
        {
            Store = new Store<ApplicationState>(Reducers.Reducers.ApplicationReducer, InitialApplicationState.Default());
        }
    }

    public interface IStatePersister<TState>
    {
        public IObjectStore<TState> StatePersister { get; init; }
    }

    public interface IWebshotPersister<TFactory> : IStatePersister<ApplicationState> where TFactory : IProjectStoreFactory
    {
        public TFactory ProjectStoreFactory { get; init; }
    }

    public sealed class FileAppStatePersister : IWebshotPersister<FileProjectStoreFactory>
    {
        private readonly string _mainDir;
        private readonly string _appStateFilename = "webshot_state.json";

        public FileAppStatePersister(string? mainDir = null)
        {
            ProjectStoreFactory = new FileProjectStoreFactory();

            _mainDir = mainDir ?? Directory.GetCurrentDirectory();
            if (!Directory.Exists(_mainDir))
            {
                throw new DirectoryNotFoundException(_mainDir);
            }
            var file = Path.Combine(_mainDir, _appStateFilename);
            StatePersister = new FileStore<ApplicationState>(file);
        }

        public FileProjectStoreFactory ProjectStoreFactory { get; init; }
        public IObjectStore<ApplicationState> StatePersister { get; init; }
    }
}