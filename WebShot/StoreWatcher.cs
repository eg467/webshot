using Redux;
using System;
using WebshotService;
using WebshotService.Entities;

namespace Webshot
{
    /// <summary>
    /// Persists changes of application state to the file store.
    /// </summary>
    public sealed class StoreWatcher : IDisposable
    {
        private readonly IStore<ApplicationState> _reduxStore;
        private readonly IObjectStore<ApplicationState> _fileStore;
        private readonly IDisposable _listener;

        public StoreWatcher(IStore<ApplicationState> reduxStore, IObjectStore<ApplicationState> fileStore)
        {
            _reduxStore = reduxStore;
            _fileStore = fileStore;
            _listener = _reduxStore.Subscribe(_fileStore.Save);
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }
    }
}