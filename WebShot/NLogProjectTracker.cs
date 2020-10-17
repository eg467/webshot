using System;
using System.IO;

namespace Webshot
{
    /// <summary>
    /// Changes project variable so NLogger will write to the current project's directory.
    /// </summary>
    internal sealed class NLogProjectTracker : IDisposable
    {
        private readonly StoreWatcher _storeWatcher;

        public NLogProjectTracker(StoreWatcher storeWatcher)
        {
            _storeWatcher = storeWatcher;
            _storeWatcher.ProjectChanged += StoreWatcher_ProjectChanged;
        }

        public void Dispose()
        {
            _storeWatcher.ProjectChanged -= StoreWatcher_ProjectChanged;
        }

        private void StoreWatcher_ProjectChanged(object? sender, EventArgs e)
        {
            var project = _storeWatcher.State.CurrentProject;
            var projectDir = project is object
                ? Path.GetDirectoryName(project.Id) ?? throw new DirectoryNotFoundException()
                : "";
            NLog.LogManager.Configuration.Variables["projectdir"] = projectDir;
        }
    }
}